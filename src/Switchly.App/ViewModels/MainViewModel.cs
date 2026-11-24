using Microsoft.Extensions.Logging;
using Switchly.App.Common;

namespace Switchly.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ILogger<MainViewModel> _logger;
    private string _address = "https://chatgpt.com";

    public MainViewModel(ILogger<MainViewModel> logger)
    {
        _logger = logger;
        NavigateCommand = new RelayCommand(_ => Navigate());
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public RelayCommand NavigateCommand { get; }

    /// <summary>Raised when navigation is requested; the View performs browser.Load.</summary>
    public event Action<string>? NavigateRequested;

    private void Navigate()
    {
        var url = _address.Trim();
        if (url.Length == 0)
        {
            return;
        }
        if (!url.Contains("://"))
        {
            url = "https://" + url;
        }
        _logger.LogInformation("Navigate: {Url}", url);
        NavigateRequested?.Invoke(url);
    }
}
