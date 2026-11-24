using System.IO;
using System.Windows;
using CefSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Diagnostics;
using Switchly.App.ViewModels;
using Switchly.Infrastructure.Persistence;

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

        // Composition root: DI + logging + persistence.
        var services = new ServiceCollection();
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwitchlyNext");
        var logDirectory = Path.Combine(appData, "Logs");
        var dataDirectory = Path.Combine(appData, "Data");
        Directory.CreateDirectory(dataDirectory);
        var dbPath = Path.Combine(dataDirectory, "switchly-next.db");

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddSwitchlyFile(logDirectory);
        });
        services.AddDbContext<SwitchlyDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<DatabaseHealthCheck>();
        services.AddSingleton<MainViewModel>();
        Services = services.BuildServiceProvider();

        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Switchly Next started.");

        using (var scope = Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<DatabaseHealthCheck>().Check();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cef.Shutdown();
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
