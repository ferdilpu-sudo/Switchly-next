using CefSharp;
using CefSharp.OffScreen;

namespace Switchly.EngineSpike;

/// <summary>
/// M-001 TASK-002 PoC: prove per-profile CefRequestContext storage isolation
/// (TEST-003) and persistence across context recreation (TEST-004).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "switchly-engine-spike", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);

        var lines = new List<string>();

        try
        {
            using var server = TestOriginServer.Start();
            lines.Add($"INFO  test origin = {server.Origin}");

            // OffScreen requires Shutdown on the same thread as Initialize; a
            // synchronous Main keeps both on the main thread (CEF runs its own
            // message loop because MultiThreadedMessageLoop defaults to true).
            Cef.EnableWaitForBrowsersToClose();
            var settings = new CefSharp.OffScreen.CefSettings
            {
                RootCachePath = baseDir,
                LogSeverity = LogSeverity.Error,
            };

            // The session runs elevated (Administrator). Chromium refuses to start its
            // sandboxed browser process at high integrity and would auto-de-elevate
            // (CEF exit code 38 / ResultCode.NormalExitAutoDeElevated), making
            // Cef.Initialize return false. This PoC only loads a loopback origin, so
            // disabling the auto-de-elevation restart is acceptable here.
            settings.CefCommandLineArgs.Add("do-not-de-elevate", "1");

            if (!Cef.Initialize(settings))
            {
                Console.WriteLine("FAIL  Cef.Initialize returned false (browser process exited early).");
                return 1;
            }

            try
            {
                // Chrome Runtime only accepts a cache_path that is a DIRECT child of
                // RootCachePath; deeper paths are rejected ("Cannot create profile").
                var cacheA = Path.Combine(baseDir, "profile-A");
                var cacheB = Path.Combine(baseDir, "profile-B");

                // TEST-003: write distinct values into two isolated contexts.
                var a1 = Probe(cacheA, "A", server.Origin);
                var b1 = Probe(cacheB, "B", server.Origin);

                // Let CEF flush cookie/localStorage state to disk.
                Thread.Sleep(1500);

                // TEST-004: fresh contexts on the SAME cache paths must reload
                // the persisted values (and still not see the other profile).
                var a2 = Probe(cacheA, null, server.Origin);
                var b2 = Probe(cacheB, null, server.Origin);

                lines.Add(Assess("A", a1, a2, "A"));
                lines.Add(Assess("B", b1, b2, "B"));
            }
            finally
            {
                Cef.WaitForBrowsersToClose();
                Cef.Shutdown();
            }
        }
        catch (Exception ex)
        {
            lines.Add("FAIL  unexpected exception: " + ex);
        }

        // Best-effort cleanup of temp profile data.
        try { Directory.Delete(baseDir, recursive: true); }
        catch { /* CEF may still hold handles; ignore */ }

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        var failed = lines.Any(l => l.StartsWith("FAIL", StringComparison.Ordinal));
        Console.WriteLine(failed ? "RESULT: FAIL" : "RESULT: PASS");
        return failed ? 1 : 0;
    }

    private static (bool Ok, string Detail) Probe(string cachePath, string? setValue, string origin)
    {
        RequestContext? requestContext = null;
        ChromiumWebBrowser? browser = null;
        try
        {
            requestContext = new RequestContext(new RequestContextSettings
            {
                CachePath = cachePath,
                PersistSessionCookies = true,
            });

            browser = new ChromiumWebBrowser(origin, requestContext: requestContext);

            var initial = browser.WaitForInitialLoadAsync()
                .WaitAsync(TimeSpan.FromSeconds(30))
                .GetAwaiter().GetResult();
            if (!initial.Success)
            {
                return (false, $"load failed (code={initial.ErrorCode}, http={initial.HttpStatusCode})");
            }

            if (setValue is not null)
            {
                var set = browser.EvaluateScriptAsync(
                        $"localStorage.setItem('probe','{setValue}');document.cookie='probe={setValue};path=/;max-age=86400';'ok'")
                    .WaitAsync(TimeSpan.FromSeconds(30))
                    .GetAwaiter().GetResult();
                if (!set.Success)
                {
                    return (false, $"set eval failed: {set.Message}");
                }
            }

            var read = browser.EvaluateScriptAsync(
                    "JSON.stringify({ls: localStorage.getItem('probe'), cookie: document.cookie})")
                .WaitAsync(TimeSpan.FromSeconds(30))
                .GetAwaiter().GetResult();

            return read.Success
                ? (true, read.Result?.ToString() ?? "<null>")
                : (false, $"read eval failed: {read.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"exception {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { browser?.Dispose(); } catch { }
            try { requestContext?.Dispose(); } catch { }
        }
    }

    private static string Assess(string name, (bool Ok, string Detail) set, (bool Ok, string Detail) reopen, string expected)
    {
        var other = expected == "A" ? "B" : "A";
        if (!set.Ok)
        {
            return $"FAIL  {name}: write/read phase error -> {set.Detail}";
        }
        if (!reopen.Ok)
        {
            return $"FAIL  {name}: reopen phase error -> {reopen.Detail}";
        }

        var setHasExpected = set.Detail.Contains($"\"ls\":\"{expected}\"", StringComparison.Ordinal)
                             && set.Detail.Contains($"probe={expected}", StringComparison.Ordinal);
        var reopenHasExpected = reopen.Detail.Contains($"\"ls\":\"{expected}\"", StringComparison.Ordinal)
                                && reopen.Detail.Contains($"probe={expected}", StringComparison.Ordinal);
        var setCross = set.Detail.Contains($"probe={other}", StringComparison.Ordinal)
                       || set.Detail.Contains($"\"ls\":\"{other}\"", StringComparison.Ordinal);
        var reopenCross = reopen.Detail.Contains($"probe={other}", StringComparison.Ordinal)
                          || reopen.Detail.Contains($"\"ls\":\"{other}\"", StringComparison.Ordinal);

        if (setHasExpected && reopenHasExpected && !setCross && !reopenCross)
        {
            return $"PASS  {name}: isolated + persisted. write=[{set.Detail}] reopen=[{reopen.Detail}]";
        }

        return $"FAIL  {name}: expected=[{expected}] write=[{set.Detail}] reopen=[{reopen.Detail}]";
    }
}
