using System.IO;
using System.Security.Principal;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Diagnostics;
using Switchly.App.ViewModels;
using Switchly.Application.Browser;
using Switchly.Application.Groups;
using Switchly.Application.Profiles;
using Switchly.BrowserHost.CefBootstrap;
using Switchly.Infrastructure.Persistence;

namespace Switchly.App;

public partial class App : System.Windows.Application
{
    private IAppInstanceGuard? _instanceGuard;
    private IProfileSessionManager? _sessionManager;
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
        services.AddDbContextFactory<SwitchlyDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddDbContext<SwitchlyDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<DatabaseHealthCheck>();
        services.AddSingleton<IAppInstanceGuard>(_ => new SingleInstanceMutexGuard(ProfilesRootPath));
        services.AddSingleton<IProfileLockManager, InProcessProfileLockManager>();
        services.AddSingleton<IProfileSessionStore, EfProfileSessionStore>();
        services.AddScoped<IProfileRepository, EfProfileRepository>();
        services.AddScoped<IGroupRepository, EfGroupRepository>();
        services.AddSingleton<IProfileStorageDirectory>(_ => new LocalProfileStorageDirectory(ProfilesRootPath, dataDirectory));
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddSingleton<IBrowserRuntime, CefBrowserRuntime>();
        services.AddSingleton<IProfileSessionManager, ProfileSessionManager>();
        services.AddSingleton<MainViewModel>();
        Services = services.BuildServiceProvider();

        // Enforce single-instance guard and process deferred profile deletions BEFORE Cef.Initialize() (ADR-011, TEST-017).
        _instanceGuard = Services.GetRequiredService<IAppInstanceGuard>();
        if (!_instanceGuard.TryAcquire())
        {
            MessageBox.Show(
                "Switchly Next sudah berjalan. Tutup instance yang sedang aktif terlebih dahulu.",
                "Switchly Next",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        Services.GetRequiredService<IProfileStorageDirectory>().ProcessPendingDeletes();

        _browserRuntime = Services.GetRequiredService<IBrowserRuntime>();
        _sessionManager = Services.GetRequiredService<IProfileSessionManager>();
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
        _sessionManager?.Dispose();
        _browserRuntime?.Shutdown();
        _instanceGuard?.Dispose();
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
