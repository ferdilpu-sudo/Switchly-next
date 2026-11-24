using System.Windows;
using Switchly.App.Common;
using Switchly.BrowserHost.BrowserAdapters;

namespace Switchly.App.ViewModels;

/// <summary>Represents a single managed browser tab inside a BrowserWindow (UI-003, REQ-005).</summary>
public sealed class BrowserTabItemViewModel : ViewModelBase
{
    private string _title = "Tab Baru";
    private string _address;
    private bool _isLoading;

    public BrowserTabItemViewModel(IWpfBrowserWindowHost host, string initialUrl)
    {
        TabId = Guid.NewGuid();
        Host = host ?? throw new ArgumentNullException(nameof(host));
        _address = initialUrl;
    }

    public Guid TabId { get; }

    public IWpfBrowserWindowHost Host { get; }

    public FrameworkElement Control => Host.Control;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, string.IsNullOrWhiteSpace(value) ? "Tab Baru" : value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
}
