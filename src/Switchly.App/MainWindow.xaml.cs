using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Switchly.App.ViewModels;
using Switchly.Application.Browser;
using Switchly.BrowserHost.BrowserAdapters;

namespace Switchly.App;

public partial class MainWindow : Window
{
    private static readonly Guid DefaultBootstrapProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly MainViewModel _viewModel;
    private readonly IBrowserProfileContext _profileContext;
    private readonly IWpfBrowserWindowHost _browserHost;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        var runtime = App.Services.GetRequiredService<IBrowserRuntime>();
        var profileStoragePath = Path.Combine(App.ProfilesRootPath, DefaultBootstrapProfileId.ToString("N"));
        _profileContext = runtime.CreateProfileContext(DefaultBootstrapProfileId, profileStoragePath, persistSessionCookies: true);
        _browserHost = (IWpfBrowserWindowHost)runtime.CreateWindowHost(_profileContext, _viewModel.Address);

        BrowserContainer.Content = _browserHost.Control;

        _viewModel.NavigateRequested += OnNavigateRequested;
        _browserHost.AddressChanged += OnAddressChanged;
        Closed += OnWindowClosed;
    }

    private void OnNavigateRequested(string url) => _browserHost.Navigate(url);

    private void OnAddressChanged(string address) => Dispatcher.Invoke(() => _viewModel.Address = address);

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.NavigateRequested -= OnNavigateRequested;
        _browserHost.AddressChanged -= OnAddressChanged;
        _browserHost.Dispose();
        _profileContext.Dispose();
    }
}
