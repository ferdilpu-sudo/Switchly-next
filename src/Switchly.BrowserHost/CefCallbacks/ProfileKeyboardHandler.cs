using CefSharp;
using Switchly.Application.Browser;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Intercepts standard desktop browser shortcuts before CEF consumes them so
/// tab management, address bar focus, reload, zoom, and Ctrl+K always work reliably.
/// </summary>
internal sealed class ProfileKeyboardHandler : IKeyboardHandler
{
    private const int VkTab = 0x09;
    private const int Vk0 = 0x30;
    private const int VkF = 0x46;
    private const int VkK = 0x4B;
    private const int VkL = 0x4C;
    private const int VkR = 0x52;
    private const int VkT = 0x54;
    private const int VkW = 0x57;
    private const int VkF5 = 0x74;
    private const int VkAdd = 0x6B;
    private const int VkSubtract = 0x6D;
    private const int VkOemPlus = 0xBB;
    private const int VkOemMinus = 0xBD;

    private readonly Action<BrowserShortcutCommand> _onShortcutPressed;

    public ProfileKeyboardHandler(Action<BrowserShortcutCommand> onShortcutPressed)
    {
        _onShortcutPressed = onShortcutPressed ?? throw new ArgumentNullException(nameof(onShortcutPressed));
    }

    public bool OnPreKeyEvent(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        KeyType type,
        int windowsKeyCode,
        int nativeKeyCode,
        CefEventFlags modifiers,
        bool isSystemKey,
        ref bool isKeyboardShortcut)
    {
        if (type != KeyType.RawKeyDown && type != KeyType.KeyDown)
        {
            return false;
        }

        var ctrl = (modifiers & CefEventFlags.ControlDown) != 0;
        var shift = (modifiers & CefEventFlags.ShiftDown) != 0;
        var alt = (modifiers & CefEventFlags.AltDown) != 0;

        if (!alt && windowsKeyCode == VkF5)
        {
            _onShortcutPressed(BrowserShortcutCommand.Reload);
            return true;
        }

        if (!ctrl || alt)
        {
            return false;
        }

        BrowserShortcutCommand? command = windowsKeyCode switch
        {
            VkT when shift => BrowserShortcutCommand.ReopenClosedTab,
            VkT when !shift => BrowserShortcutCommand.NewTab,
            VkW when !shift => BrowserShortcutCommand.CloseTab,
            VkL when !shift => BrowserShortcutCommand.FocusAddressBar,
            VkF when !shift => BrowserShortcutCommand.FindInPage,
            VkK when !shift => BrowserShortcutCommand.QuickSearch,
            VkR when !shift => BrowserShortcutCommand.Reload,
            VkTab when !shift => BrowserShortcutCommand.NextTab,
            VkOemPlus or VkAdd => BrowserShortcutCommand.ZoomIn,
            VkOemMinus or VkSubtract => BrowserShortcutCommand.ZoomOut,
            Vk0 when !shift => BrowserShortcutCommand.ResetZoom,
            _ => null,
        };

        if (command is null)
        {
            return false;
        }

        isKeyboardShortcut = true;
        _onShortcutPressed(command.Value);
        return true;
    }

    public bool OnKeyEvent(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        KeyType type,
        int windowsKeyCode,
        int nativeKeyCode,
        CefEventFlags modifiers,
        bool isSystemKey) => false;
}
