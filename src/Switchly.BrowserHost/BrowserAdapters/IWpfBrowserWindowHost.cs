using System.Windows;
using Switchly.Application.Browser;

namespace Switchly.BrowserHost.BrowserAdapters;

/// <summary>WPF-facing browser host contract exposing the embeddable visual control without leaking CefSharp types.</summary>
public interface IWpfBrowserWindowHost : IBrowserWindowHost
{
    FrameworkElement Control { get; }
}
