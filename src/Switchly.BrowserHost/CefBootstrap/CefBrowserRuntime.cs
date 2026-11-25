using System.IO;
using CefSharp;
using CefSharp.Wpf;
using Switchly.Application.Browser;
using Switchly.BrowserHost.BrowserAdapters;

namespace Switchly.BrowserHost.CefBootstrap;

/// <summary>Sole owner of CefSharp WPF runtime initialization, shutdown, and profile context creation (ADR-003, ADR-011).</summary>
public sealed class CefBrowserRuntime : IBrowserRuntime
{
    private string? _rootCachePath;
    private bool _initialized;

    public bool Initialize(string rootCachePath, string? logDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(rootCachePath))
        {
            throw new ArgumentException("RootCachePath is required.", nameof(rootCachePath));
        }

        var fullRoot = Path.GetFullPath(rootCachePath);
        Directory.CreateDirectory(fullRoot);

        var resolvedLogDir = string.IsNullOrWhiteSpace(logDirectory) ? fullRoot : Path.GetFullPath(logDirectory);
        Directory.CreateDirectory(resolvedLogDir);

        var settings = new CefSettings
        {
            RootCachePath = fullRoot,
            LogFile = Path.Combine(resolvedLogDir, "cef-runtime.log"),
            LogSeverity = LogSeverity.Error,
        };

#if DEBUG
        // Local elevated dev sessions would otherwise trigger CEF auto-de-elevation (exit code 38).
        // Never ship do-not-de-elevate in Release builds (rules.md §5).
        settings.CefCommandLineArgs.Add("do-not-de-elevate", "1");
#endif

        _initialized = Cef.Initialize(settings);
        if (_initialized)
        {
            _rootCachePath = fullRoot;
        }

        return _initialized;
    }

    public void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        _initialized = false;
        Cef.Shutdown();
    }

    public IBrowserProfileContext CreateProfileContext(Guid profileId, string storagePath, bool persistSessionCookies = true)
    {
        if (!_initialized || _rootCachePath is null)
        {
            throw new InvalidOperationException("Browser runtime must be initialized before creating profile contexts.");
        }

        return new CefProfileContext(profileId, storagePath, _rootCachePath, persistSessionCookies);
    }

    public IBrowserWindowHost CreateWindowHost(IBrowserProfileContext profileContext, string initialUrl)
    {
        if (profileContext is not CefProfileContext cefContext)
        {
            throw new ArgumentException("Profile context must be created by CefBrowserRuntime.", nameof(profileContext));
        }

        return new CefWpfWindowHost(cefContext, initialUrl);
    }
}
