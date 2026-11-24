using System.Diagnostics;
using CefSharp;
using CefSharp.OffScreen;

namespace Switchly.EngineSpike;

/// <summary>
/// M-001 TASK-002 PoC: prove per-profile CefRequestContext storage isolation
/// (TEST-003), persistence across context recreation (TEST-004), and the
/// process singleton lock on RootCachePath (TEST-017).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string? root = null;
        int holdSeconds = 0;
        string? readyMarker = null;
        bool singletonTest = false;
        bool noProbe = false;
        bool task004 = false;
        bool task005 = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root" when i + 1 < args.Length:
                    root = args[++i];
                    break;
                case "--hold" when i + 1 < args.Length:
                    holdSeconds = int.Parse(args[++i]);
                    break;
                case "--ready-marker" when i + 1 < args.Length:
                    readyMarker = args[++i];
                    break;
                case "--singleton-test":
                    singletonTest = true;
                    break;
                case "--no-probe":
                    noProbe = true;
                    break;
                case "--task-004":
                    task004 = true;
                    break;
                case "--task-005":
                    task005 = true;
                    break;
            }
        }

        if (singletonTest)
        {
            if (root is null)
            {
                Console.WriteLine("FAIL  --singleton-test requires --root");
                return 2;
            }
            return SingletonTest(root);
        }

        if (task004)
        {
            var task4Root = root ?? Path.Combine(Path.GetTempPath(), "switchly-engine-spike-task4", Guid.NewGuid().ToString("N"));
            return Task004.Run(task4Root);
        }

        if (task005)
        {
            var task5Root = root ?? Path.Combine(Path.GetTempPath(), "switchly-engine-spike-task5", Guid.NewGuid().ToString("N"));
            return Task005.Run(task5Root);
        }

        var baseDir = root ?? Path.Combine(Path.GetTempPath(), "switchly-engine-spike", Guid.NewGuid().ToString("N"));
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
                var code = Cef.GetExitCode();
                Console.WriteLine($"FAIL  Cef.Initialize returned false (exit code {(int)code} ({code})).");
                return 1;
            }

            if (readyMarker is not null)
            {
                File.WriteAllText(readyMarker, "ready");
            }

            try
            {
                if (!noProbe)
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
            }
            finally
            {
                if (holdSeconds > 0)
                {
                    Console.WriteLine($"INFO  holding {holdSeconds}s...");
                    Thread.Sleep(holdSeconds * 1000);
                }

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

    /// <summary>
    /// TEST-017: launch a holder process that initializes CEF on a fixed
    /// RootCachePath, then attempt a second Cef.Initialize on the same path from
    /// this process. The second initialize must fail (CEF 120+ singleton lock).
    /// </summary>
    private static int SingletonTest(string root)
    {
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, ".ready");
        File.Delete(marker);

        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine this process path.");
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("--root");
        psi.ArgumentList.Add(root);
        psi.ArgumentList.Add("--no-probe");
        psi.ArgumentList.Add("--hold");
        psi.ArgumentList.Add("15");
        psi.ArgumentList.Add("--ready-marker");
        psi.ArgumentList.Add(marker);

        using var holder = Process.Start(psi) ?? throw new InvalidOperationException("Could not start holder process.");

        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (!File.Exists(marker) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(200);
        }

        if (!File.Exists(marker))
        {
            Console.WriteLine("FAIL  TEST-017: holder process did not initialize CEF in time.");
            try { holder.Kill(entireProcessTree: true); } catch { }
            return 1;
        }

        Console.WriteLine("INFO  holder initialized CEF; attempting a second Cef.Initialize on the same RootCachePath...");

        Cef.EnableWaitForBrowsersToClose();
        var settings = new CefSharp.OffScreen.CefSettings
        {
            RootCachePath = root,
            LogSeverity = LogSeverity.Error,
        };
        settings.CefCommandLineArgs.Add("do-not-de-elevate", "1");

        var initialized = Cef.Initialize(settings);

        if (initialized)
        {
            Console.WriteLine("FAIL  TEST-017: second process initialized CEF on the same RootCachePath (no singleton lock enforced).");
            Cef.Shutdown();
        }
        else
        {
            var code = Cef.GetExitCode();
            Console.WriteLine($"PASS  TEST-017: second process blocked by singleton lock. Cef.Initialize=false, exit code={(int)code} ({code}).");
        }

        try { holder.Kill(entireProcessTree: true); } catch { }
        try { Directory.Delete(root, recursive: true); } catch { }

        return initialized ? 1 : 0;
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
