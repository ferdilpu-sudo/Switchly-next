using CefSharp;
using Switchly.Domain.Browser;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Enhances CEF's native web context menu with profile-scoped Open Link in New Tab,
/// Copy Link Address, and Inspect Element (DevTools).
/// </summary>
internal sealed class ProfileContextMenuHandler : IContextMenuHandler
{
    private const CefMenuCommand OpenLinkInNewTabCommand = (CefMenuCommand)26501;
    private const CefMenuCommand CopyLinkAddressCommand = (CefMenuCommand)26502;
    private const CefMenuCommand InspectElementCommand = (CefMenuCommand)26503;

    private readonly Action<string> _onOpenLinkInNewTab;
    private readonly Action<string> _onCopyText;

    public ProfileContextMenuHandler(Action<string> onOpenLinkInNewTab, Action<string> onCopyText)
    {
        _onOpenLinkInNewTab = onOpenLinkInNewTab ?? throw new ArgumentNullException(nameof(onOpenLinkInNewTab));
        _onCopyText = onCopyText ?? throw new ArgumentNullException(nameof(onCopyText));
    }

    public void OnBeforeContextMenu(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IContextMenuParams parameters,
        IMenuModel model)
    {
        if (!string.IsNullOrWhiteSpace(parameters.LinkUrl)
            && BrowserUrlPolicy.IsAllowedNavigationUrl(parameters.LinkUrl))
        {
            model.InsertItemAt(0, OpenLinkInNewTabCommand, "Buka link di tab baru");
            model.InsertItemAt(1, CopyLinkAddressCommand, "Salin alamat link");
            model.InsertSeparatorAt(2);
        }

        if (model.Count > 0)
        {
            model.AddSeparator();
        }

        model.AddItem(InspectElementCommand, "Inspeksi Elemen (DevTools)");
    }

    public bool OnContextMenuCommand(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IContextMenuParams parameters,
        CefMenuCommand commandId,
        CefEventFlags eventFlags)
    {
        switch (commandId)
        {
            case OpenLinkInNewTabCommand when BrowserUrlPolicy.IsAllowedNavigationUrl(parameters.LinkUrl):
                _onOpenLinkInNewTab(parameters.LinkUrl);
                return true;

            case CopyLinkAddressCommand when !string.IsNullOrWhiteSpace(parameters.LinkUrl):
                _onCopyText(parameters.LinkUrl);
                return true;

            case InspectElementCommand:
                browser.ShowDevTools(inspectElementAtX: parameters.XCoord, inspectElementAtY: parameters.YCoord);
                return true;

            default:
                return false;
        }
    }

    public void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
    {
    }

    public bool RunContextMenu(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IContextMenuParams parameters,
        IMenuModel model,
        IRunContextMenuCallback callback) => false;
}
