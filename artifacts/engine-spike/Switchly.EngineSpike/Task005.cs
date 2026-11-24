using System.Diagnostics;
using CefSharp;
using CefSharp.OffScreen;

namespace Switchly.EngineSpike;

/// <summary>
/// M-001 TASK-005: measure process count and memory at CEF startup, 1/3/5/10
/// active profiles, and after closing them (TEST-016, NFR-PERF-001/004).
/// Informational baseline — no pass/fail thresholds are invented.
/// </summary>
internal static class Task005
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        PrintDeviceSpec();

        var lines = new List<string>();
        var profiles = new List<(RequestContext Ctx, ChromiumWebBrowser Browser)>();
        var total = Stopwatch.StartNew();

        try
        {
            using var server = TestOriginServer.Start();

            var init = Stopwatch.StartNew();
            Cef.EnableWaitForBrowsersToClose();
            var settings = new CefSharp.OffScreen.CefSettings { RootCachePath = root, LogSeverity = LogSeverity.Error };
            settings.CefCommandLineArgs.Add("do-not-de-elevate", "1");
            if (!Cef.Initialize(settings))
            {
                Console.WriteLine($"FAIL  Cef.Initialize false (exit {(int)Cef.GetExitCode()})");
                return 1;
            }
            init.Stop();

            try
            {
                lines.Add($"startup: Cef.Initialize={init.ElapsedMilliseconds}ms");
                lines.Add(Snapshot("cef-startup (0 profiles)"));

                OpenProfiles(profiles, server.Origin, root, 1, 0);
                lines.Add(Snapshot("1 active profile"));

                OpenProfiles(profiles, server.Origin, root, 2, 1);
                lines.Add(Snapshot("3 active profiles"));

                OpenProfiles(profiles, server.Origin, root, 2, 3);
                lines.Add(Snapshot("5 active profiles"));

                OpenProfiles(profiles, server.Origin, root, 5, 5);
                lines.Add(Snapshot("10 active profiles"));

                // NFR-PERF-001/004: closing all profiles must free renderer processes.
                foreach (var (_, b) in profiles)
                {
                    try { b.Dispose(); } catch { }
                }
                profiles.Clear();
                Thread.Sleep(2000);
                lines.Add(Snapshot("all profiles closed"));
            }
            finally
            {
                Cef.WaitForBrowsersToClose();
                Cef.Shutdown();
            }
        }
        catch (Exception ex)
        {
            lines.Add("FAIL  unexpected: " + ex);
        }

        total.Stop();
        lines.Add($"total elapsed: {total.ElapsedMilliseconds}ms");

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
        Console.WriteLine("RESULT: MEASURED (baseline report; no threshold applied)");
        return 0;
    }

    private static void OpenProfiles(List<(RequestContext Ctx, ChromiumWebBrowser Browser)> profiles, string origin, string root, int count, int startIndex)
    {
        for (int i = 0; i < count; i++)
        {
            var idx = startIndex + i;
            var ctx = new RequestContext(new RequestContextSettings
            {
                CachePath = Path.Combine(root, $"profile-{idx}"),
                PersistSessionCookies = true,
            });
            var browser = new ChromiumWebBrowser(origin, requestContext: ctx);
            browser.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            profiles.Add((ctx, browser));
        }
    }

    private static string Snapshot(string label)
    {
        var procs = Process.GetProcesses()
            .Where(p =>
            {
                try
                {
                    var n = p.ProcessName;
                    return n.Contains("CefSharp.BrowserSubprocess") || n.Contains("Switchly.EngineSpike");
                }
                catch { return false; }
            })
            .ToList();

        long priv = 0, ws = 0;
        foreach (var p in procs)
        {
            try { priv += p.PrivateMemorySize64; } catch { }
            try { ws += p.WorkingSet64; } catch { }
        }

        return $"{label}: procs={procs.Count} private={priv / (1024 * 1024)}MB workingSet={ws / (1024 * 1024)}MB";
    }

    private static void PrintDeviceSpec()
    {
        Console.WriteLine($"device: OS={Environment.OSVersion} logicalProc={Environment.ProcessorCount} x64={(Environment.Is64BitProcess ? "yes" : "no")}");
    }
}
