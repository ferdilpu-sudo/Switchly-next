using System.IO;
using System.Security.Principal;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Diagnostics;
using Switchly.App.Navigation;
using Switchly.App.Themes;
using Switchly.App.ViewModels;
using Switchly.App.Views;
using Switchly.Application.Browser;
using Switchly.Application.Diagnostics;
using Switchly.Application.Entitlements;
using Switchly.Application.Groups;
using Switchly.Application.Import;
using Switchly.Application.Profiles;
using Switchly.Application.Security;
using Switchly.Application.Vault;
using Switchly.BrowserHost.CefBootstrap;
using Switchly.Infrastructure.Diagnostics;
using Switchly.Infrastructure.Entitlements;
using Switchly.Infrastructure.Import;
using Switchly.Infrastructure.Persistence;
using Switchly.Infrastructure.Security;

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

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                Services?.GetService<ILogger<App>>()?.LogError(args.Exception, "Unhandled UI exception intercepted.");
            }
            catch
            {
            }

            MessageBox.Show(
                $"Terjadi kesalahan tak terduga:\n{args.Exception.Message}",
                "Switchly",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                Services?.GetService<ILogger<App>>()?.LogError(args.Exception, "Unobserved background task exception.");
            }
            catch
            {
            }

            args.SetObserved();
        };

#if !DEBUG
        if (IsRunningAsAdministrator())
        {
            MessageBox.Show(
                "Switchly (Release) tidak dapat dijalankan sebagai Administrator karena kebijakan keamanan CEF (exit code 38). Jalankan sebagai user standar (non-elevated), atau gunakan build Debug untuk pengujian lokal.",
                "Switchly",
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
        ThemeManager.Initialize(dataDirectory);
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                {
                    ThemeManager.ApplyWindowChrome(window);
                }
            }));
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
        var licenseConfig = HttpLicenseApiClient.LoadConfigFromDiskOrEnvironment(dataDirectory);
        services.AddSingleton(licenseConfig);
        services.AddSingleton<ILocalDataProtector, WindowsDpapiProtector>();
        services.AddSingleton<ILicenseStore>(sp =>
            new DpapiLicenseStore(Path.Combine(dataDirectory, "license.dpapi"), sp.GetRequiredService<ILocalDataProtector>()));
        services.AddSingleton<IDeviceFingerprintProvider>(_ => new WindowsDeviceFingerprintProvider(dataDirectory));
        services.AddSingleton<ILicenseApiClient>(_ => new HttpLicenseApiClient(licenseConfig));
        if (licenseConfig.IsConfigured && licenseConfig.EnforceCommercialLimits)
        {
            services.AddSingleton<ILicenseTokenVerifier>(_ =>
                new Ed25519LicenseTokenVerifier(licenseConfig.Ed25519PublicKeyBase64));
            services.AddSingleton<IFeatureEntitlementService, SignedTokenEntitlementService>();
        }
        else
        {
            services.AddSingleton<IFeatureEntitlementService, FreeBetaEntitlementService>();
        }

        services.AddSingleton<LicenseAccountService>();
        services.AddSingleton<IAppInstanceGuard>(_ => new SingleInstanceMutexGuard(ProfilesRootPath));
        services.AddSingleton<IProfileLockManager, InProcessProfileLockManager>();
        services.AddSingleton<IProfileSessionStore, EfProfileSessionStore>();
        services.AddScoped<IProfileRepository, EfProfileRepository>();
        services.AddScoped<IGroupRepository, EfGroupRepository>();
        services.AddScoped<IBookmarkRepository, EfBookmarkRepository>();
        services.AddScoped<IHistoryRepository, EfHistoryRepository>();
        services.AddScoped<IDownloadRepository, EfDownloadRepository>();
        services.AddScoped<ITabSessionRepository, EfTabSessionRepository>();
        services.AddScoped<IImportJobRepository, EfImportJobRepository>();
        services.AddScoped<IBackupJobRepository, EfBackupJobRepository>();
        services.AddScoped<IVaultCredentialRepository, EfVaultCredentialRepository>();
        services.AddSingleton<ILocalSecurityService>(sp =>
            new LocalSecurityService(sp.GetRequiredService<ILocalDataProtector>(), dataDirectory));
        services.AddScoped<IPasswordVaultService>(sp =>
            new PasswordVaultService(
                sp.GetRequiredService<IVaultCredentialRepository>(),
                sp.GetRequiredService<ILocalDataProtector>(),
                dataDirectory));
        services.AddScoped<IBackupRestoreService>(sp =>
            new PortableBackupRestoreService(
                appData,
                sp.GetRequiredService<IProfileRepository>(),
                sp.GetRequiredService<IBackupJobRepository>()));
        services.AddSingleton<IProfileStorageDirectory>(_ => new LocalProfileStorageDirectory(ProfilesRootPath, dataDirectory));
        services.AddSingleton<IBrowserProfileDiscovery, ChromiumProfileDiscovery>();
        services.AddSingleton<IBookmarkImporter, ChromiumBookmarkImporter>();
        services.AddSingleton<IHistoryImporter, ChromiumHistoryImporter>();
        services.AddSingleton<IOpenTabImporter, ChromiumOpenTabImporter>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IBookmarkService, BookmarkService>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IDownloadService, DownloadService>();
        services.AddScoped<ITabSessionService, TabSessionService>();
        services.AddScoped<IBrowserImportService, BrowserImportService>();
        services.AddSingleton<IResourceMonitor, WindowsResourceMonitor>();
        services.AddSingleton<IBrowserRuntime, CefBrowserRuntime>();
        services.AddSingleton<IProfileSessionManager, ProfileSessionManager>();
        services.AddSingleton<BrowserWindowCoordinator>();
        services.AddSingleton<WorkspaceViewModel>();
        Services = services.BuildServiceProvider();

        // Enforce single-instance guard and process deferred profile deletions BEFORE Cef.Initialize() (ADR-011, TEST-017).
        _instanceGuard = Services.GetRequiredService<IAppInstanceGuard>();
        if (!_instanceGuard.TryAcquire())
        {
            MessageBox.Show(
                "Switchly sudah berjalan. Tutup instance yang sedang aktif terlebih dahulu.",
                "Switchly",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        // 1. Apply pending restart-to-restore staging swap BEFORE Cef.Initialize() and EF Core open any handles (ADR-006, D3).
        try
        {
            PendingRestoreApplier.ApplyPendingRestoreIfAny(appData);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Pemulihan backup gagal diterapkan dan telah dikembalikan ke data sebelumnya.\n\nDetail: {ex.Message}",
                "Switchly — Pemulihan Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // 2. Process deferred profile deletions & sweep any stale plaintext backup/restore temp files BEFORE Cef.Initialize() (ADR-011).
        PortableBackupRestoreService.SweepStaleTempFiles(dataDirectory);
        Services.GetRequiredService<IProfileStorageDirectory>().ProcessPendingDeletes();

        // 3. Enforce local DPAPI health and optional master-password unlock BEFORE opening workspace (REQ-010, TEST-011).
        var securityService = Services.GetRequiredService<ILocalSecurityService>();
        var securityStatus = securityService.GetStatus();
        if (!securityStatus.IsProtectionHealthy)
        {
            MessageBox.Show(
                securityStatus.ErrorMessage ?? "Perlindungan keamanan lokal gagal dibuka (DPAPI fail-closed).",
                "Switchly — Keamanan Lokal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        if (securityStatus.IsMasterPasswordEnabled)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var unlockDialog = new UnlockDialog(securityService);
            if (unlockDialog.ShowDialog() != true)
            {
                Shutdown(0);
                return;
            }
        }

        _browserRuntime = Services.GetRequiredService<IBrowserRuntime>();
        _sessionManager = Services.GetRequiredService<IProfileSessionManager>();
        if (!_browserRuntime.Initialize(ProfilesRootPath, logDirectory))
        {
            MessageBox.Show(
                "Embedded browser gagal dimulai. Pastikan Switchly belum berjalan di instance lain dan tidak dijalankan sebagai Administrator.",
                "Switchly",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Switchly started.");

        using (var scope = Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<DatabaseHealthCheck>().Check();
            scope.ServiceProvider.GetRequiredService<IDownloadService>()
                .MarkInterruptedOnStartupAsync()
                .GetAwaiter()
                .GetResult();
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
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
