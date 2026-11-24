using System.Windows;
using CefSharp;

namespace Switchly.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = new CefSharp.Wpf.CefSettings
        {
            LogSeverity = LogSeverity.Error,
        };

        // The dev session runs elevated; CEF would otherwise auto-de-elevate
        // (exit code 38) and fail to start. Production must run asInvoker.
        settings.CefCommandLineArgs.Add("do-not-de-elevate", "1");

        if (!Cef.Initialize(settings))
        {
            MessageBox.Show(
                "Cef.Initialize returned false — the embedded browser failed to start.",
                "Switchly Next",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cef.Shutdown();
        base.OnExit(e);
    }
}
