using CefSharp;
using CefSharp.OffScreen;

namespace Switchly.EngineSpike;

/// <summary>
/// M-001 TASK-003 (tier 1): unauthenticated public-page smoke check of the four
/// priority providers. Records load status, final URL (redirect detection) and
/// page title. No accounts, no sign-in, no user-agent spoofing, no bypassing.
/// </summary>
internal static class Task003
{
    private static readonly (string Name, string Url)[] Sites =
    {
        ("Google Flow", "https://flow.google"),
        ("ChatGPT", "https://chatgpt.com"),
        ("Dola", "https://dola.com"),
        ("SeaArt", "https://seaart.ai"),
    };

    private static readonly (string Name, string Url)[] SignInSites =
    {
        ("Google sign-in", "https://accounts.google.com/v3/signin/identifier"),
        ("ChatGPT login", "https://chatgpt.com/auth/login"),
    };

    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        var lines = new List<string>();

        try
        {
            Cef.EnableWaitForBrowsersToClose();
            var settings = new CefSharp.OffScreen.CefSettings { RootCachePath = root, LogSeverity = LogSeverity.Error };
            settings.CefCommandLineArgs.Add("do-not-de-elevate", "1"); // elevated session only; no UA spoofing
            if (!Cef.Initialize(settings))
            {
                Console.WriteLine($"FAIL  Cef.Initialize false (exit {(int)Cef.GetExitCode()})");
                return 1;
            }

            // Let CEF's context initialization finish before creating the first
            // browser (avoids an intermittent native crash in CreateBrowser).
            Thread.Sleep(1500);

            try
            {
                var ctx = new RequestContext(new RequestContextSettings
                {
                    CachePath = Path.Combine(root, "profile-main"),
                    PersistSessionCookies = true,
                });

                foreach (var (name, url) in Sites)
                {
                    lines.Add(Probe(name, url, ctx));
                }

                lines.Add("--- sign-in reachability (non-account, no credentials) ---");
                foreach (var (name, url) in SignInSites)
                {
                    lines.Add(Probe(name, url, ctx));
                }
                lines.Add("NOTE  Dola/SeaArt sign-in is SPA-gated (behind JS buttons) — not reachable headlessly without user interaction.");
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
        Console.WriteLine("RESULT: REPORTED");
        return 0;
    }

    private static string Probe(string name, string url, RequestContext ctx)
    {
        var browser = new ChromiumWebBrowser("about:blank", requestContext: ctx);
        try
        {
            browser.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();

            var resp = browser.LoadUrlAsync(url).WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();

            // Allow JS-rendered pages to settle and set their title.
            Thread.Sleep(4000);

            var finalUrl = browser.Address;
            var title = ReadTitle(browser);

            return $"{name}: success={resp.Success} error={resp.ErrorCode} http={resp.HttpStatusCode} finalUrl='{finalUrl}' title='{Truncate(title)}'";
        }
        catch (TimeoutException)
        {
            return $"{name}: TIMEOUT (>45s) url='{browser.Address}'";
        }
        catch (Exception ex)
        {
            return $"{name}: exception {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            browser.Dispose();
        }
    }

    private static string ReadTitle(ChromiumWebBrowser browser)
    {
        try
        {
            if (!browser.CanExecuteJavascriptInMainFrame)
            {
                return "<no-v8-context>";
            }
            var r = browser.EvaluateScriptAsync("document.title").WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            return r.Success ? (r.Result?.ToString() ?? "") : $"<eval:{r.Message}>";
        }
        catch
        {
            return "<title-read-failed>";
        }
    }

    private static string Truncate(string s)
    {
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= 80 ? s : s.Substring(0, 77) + "...";
    }
}
