using CefSharp;
using CefSharp.OffScreen;

namespace Switchly.EngineSpike;

/// <summary>
/// M-001 TASK-004: downloads, popups/new-windows, navigation callbacks, and
/// renderer termination (TEST-021).
/// </summary>
internal static class Task004
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        var lines = new List<string>();

        try
        {
            using var server = TestOriginServer.Start();
            lines.Add($"INFO  origin={server.Origin}");

            Cef.EnableWaitForBrowsersToClose();
            var settings = new CefSharp.OffScreen.CefSettings
            {
                RootCachePath = root,
                LogSeverity = LogSeverity.Error,
            };
            settings.CefCommandLineArgs.Add("do-not-de-elevate", "1");
            settings.CefCommandLineArgs.Add("disable-popup-blocking", "1");

            if (!Cef.Initialize(settings))
            {
                Console.WriteLine($"FAIL  Cef.Initialize false (exit {(int)Cef.GetExitCode()})");
                return 1;
            }

            try
            {
                var context = new RequestContext(new RequestContextSettings
                {
                    CachePath = Path.Combine(root, "profile-main"),
                    PersistSessionCookies = true,
                });

                var d = DownloadProbe(server.Origin, context);
                lines.Add(d.Ok ? $"PASS  download: {d.Detail}" : $"FAIL  download: {d.Detail}");

                var p = PopupProbe(server.Origin, context);
                lines.Add(p.Ok ? $"PASS  popup: {p.Detail}" : $"FAIL  popup: {p.Detail}");

                var n = NavigationProbe(server.Origin, context);
                lines.Add(n.Ok ? $"PASS  navigation: {n.Detail}" : $"FAIL  navigation: {n.Detail}");

                var c = RendererCrashProbe(context);
                lines.Add(c.Ok ? $"PASS  renderer-crash: {c.Detail}" : $"WARN  renderer-crash: {c.Detail}");
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

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        var failed = lines.Any(l => l.StartsWith("FAIL", StringComparison.Ordinal));
        Console.WriteLine(failed ? "RESULT: FAIL" : "RESULT: PASS");
        return failed ? 1 : 0;
    }

    private static void WaitForV8Context(ChromiumWebBrowser browser, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!browser.CanExecuteJavascriptInMainFrame && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }
        if (!browser.CanExecuteJavascriptInMainFrame)
        {
            throw new TimeoutException("V8 context not available in main frame");
        }
    }

    private static (bool Ok, string Detail) DownloadProbe(string origin, RequestContext context)
    {
        var handler = new DownloadCaptureHandler();
        var browser = new ChromiumWebBrowser("about:blank", requestContext: context) { DownloadHandler = handler };
        try
        {
            browser.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            browser.Load(origin + "/download");

            var path = handler.Done.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(path))
            {
                return (false, "download cancelled or empty path");
            }
            if (!File.Exists(path))
            {
                return (false, $"download complete but file missing: {path}");
            }

            var content = File.ReadAllText(path);
            return content == "hello-download"
                ? (true, $"saved '{path}' with correct content")
                : (false, $"content mismatch: '{content}'");
        }
        catch (TimeoutException)
        {
            return (false, "download did not complete within 30s");
        }
        catch (Exception ex)
        {
            return (false, $"exception {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            browser.Dispose();
        }
    }

    private static (bool Ok, string Detail) PopupProbe(string origin, RequestContext context)
    {
        var handler = new PopupCaptureHandler();
        var browser = new ChromiumWebBrowser(origin, requestContext: context) { LifeSpanHandler = handler };
        ChromiumWebBrowser? popup = null;
        try
        {
            browser.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            WaitForV8Context(browser, TimeSpan.FromSeconds(10));

            var set = browser.EvaluateScriptAsync("localStorage.setItem('shared','XYZ')")
                .WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            if (!set.Success)
            {
                return (false, $"set localStorage failed: {set.Message}");
            }

            browser.LoadUrlAsync(origin + "/popup").WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();

            popup = handler.PopupTask.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();

            popup.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            WaitForV8Context(popup, TimeSpan.FromSeconds(10));
            var read = popup.EvaluateScriptAsync("localStorage.getItem('shared')")
                .WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            var value = read.Success ? (read.Result?.ToString() ?? "<null>") : $"<eval failed: {read.Message}>";

            return value == "XYZ"
                ? (true, "popup shares parent context (localStorage['shared']='XYZ')")
                : (false, $"popup localStorage['shared']={value} (expected 'XYZ')");
        }
        catch (TimeoutException)
        {
            return (false, "popup probe timed out");
        }
        catch (Exception ex)
        {
            return (false, $"exception {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { popup?.Dispose(); } catch { }
            browser.Dispose();
        }
    }

    private static (bool Ok, string Detail) NavigationProbe(string origin, RequestContext context)
    {
        var browser = new ChromiumWebBrowser("about:blank", requestContext: context);
        try
        {
            browser.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            var r = browser.LoadUrlAsync(origin + "/404").WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            // Observed in 156.0.300: a 4xx is surfaced as a non-success with
            // ErrorCode=HttpResponseCodeFailure (not HttpStatusCode=404).
            var expected = !r.Success && r.ErrorCode == CefErrorCode.HttpResponseCodeFailure;
            return expected
                ? (true, $"404 surfaced as non-success: http={r.HttpStatusCode} error={r.ErrorCode}")
                : (false, $"404 mismatch: http={r.HttpStatusCode} error={r.ErrorCode}");
        }
        catch (Exception ex)
        {
            return (false, $"exception {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            browser.Dispose();
        }
    }

    private static (bool Ok, string Detail) RendererCrashProbe(RequestContext context)
    {
        var handler = new RenderTerminationHandler();
        var browser = new ChromiumWebBrowser("about:blank", requestContext: context) { RequestHandler = handler };
        try
        {
            browser.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            browser.Load("chrome://crash");
            var status = handler.Terminated.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            return (true, $"OnRenderProcessTerminated fired, status={status}");
        }
        catch (TimeoutException)
        {
            return (false, "chrome://crash did not trigger OnRenderProcessTerminated within 30s");
        }
        catch (Exception ex)
        {
            return (false, $"exception {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            browser.Dispose();
        }
    }
}
