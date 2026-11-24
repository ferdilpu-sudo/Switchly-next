using System.IO;
using System.Windows;
using CefSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Diagnostics;
using Switchly.App.ViewModels;

namespace Switchly.App;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

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
            return;
        }

        // Composition root: DI + logging.
        var services = new ServiceCollection();
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwitchlyNext",
            "Logs");
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddSwitchlyFile(logDirectory);
        });
        services.AddSingleton<MainViewModel>();
        Services = services.BuildServiceProvider();

        Services.GetRequiredService<ILogger<App>>().LogInformation("Switchly Next started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cef.Shutdown();
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
