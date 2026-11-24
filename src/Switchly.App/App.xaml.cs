using System.IO;
using System.Security.Principal;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Diagnostics;
using Switchly.App.ViewModels;
using Switchly.Application.Browser;
using Switchly.BrowserHost.CefBootstrap;
using Switchly.Infrastructure.Persistence;

namespace Switchly.App;

public partial class App : System.Windows.Application
{
    private IBrowserRuntime? _browserRuntime;

    public static IServiceProvider Services { get; private set; } = null!;

    public static string ProfilesRootPath { get; private set; } = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

#if !DEBUG
        if (IsRunningAsAdministrator())
        {
            MessageBox.Show(
                "Switchly Next (Release) tidak dapat dijalankan sebagai Administrator karena kebijakan keamanan CEF (exit code 38). Jalankan sebagai user standar (non-elevated), atau gunakan build Debug untuk pengujian lokal.",
                "Switchly Next",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(38);
            return;
        }
#endif

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwitchlyNext");
        var logDirectory = Path.Combine(appData, "Logs");
        var dataDirectory = Path.Combine(appData, "Data");
        ProfilesRootPath = Path.Combine(appData, "Profiles");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(ProfilesRootPath);
        var dbPath = Path.Combine(dataDirectory, "switchly-next.db");

        // Composition root: DI + logging + persistence + browser host.
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddSwitchlyFile(logDirectory);
        });
        services.AddDbContext<SwitchlyDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<DatabaseHealthCheck>();
        services.AddSingleton<IBrowserRuntime, CefBrowserRuntime>();
        services.AddSingleton<MainViewModel>();
        Services = services.BuildServiceProvider();

        _browserRuntime = Services.GetRequiredService<IBrowserRuntime>();
        if (!_browserRuntime.Initialize(ProfilesRootPath))
        {
            MessageBox.Show(
                "Embedded browser gagal dimulai. Pastikan Switchly Next belum berjalan di instance lain dan tidak dijalankan sebagai Administrator.",
                "Switchly Next",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Switchly Next started.");

        using (var scope = Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<DatabaseHealthCheck>().Check();
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _browserRuntime?.Shutdown();
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
