using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Switchly.App.Themes;

public enum AppTheme
{
    Dark = 0,
    Light = 1,
}

public static class ThemeManager
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private static readonly Uri DarkThemeUri = new("Themes/DarkTheme.xaml", UriKind.Relative);
    private static readonly Uri LightThemeUri = new("Themes/LightTheme.xaml", UriKind.Relative);

    private static string? _preferenceFilePath;

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static event EventHandler<AppTheme>? ThemeChanged;

    public static void Initialize(string dataDirectory)
    {
        _preferenceFilePath = Path.Combine(dataDirectory, "ui-theme.txt");
        var initialTheme = AppTheme.Light;

        try
        {
            if (File.Exists(_preferenceFilePath))
            {
                var saved = File.ReadAllText(_preferenceFilePath).Trim();
                if (Enum.TryParse<AppTheme>(saved, ignoreCase: true, out var parsed))
                {
                    initialTheme = parsed;
                }
            }
        }
        catch
        {
            // Fall back to default Dark theme if preference file is unreadable.
        }

        ApplyTheme(initialTheme, persist: false);
    }

    public static void ToggleTheme()
    {
        var next = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme(next, persist: true);
    }

    public static void ApplyTheme(AppTheme theme, bool persist = true)
    {
        CurrentTheme = theme;

        var app = System.Windows.Application.Current;
        if (app is not null)
        {
            var merged = app.Resources.MergedDictionaries;
            var newDict = new ResourceDictionary
            {
                Source = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri,
            };

            if (merged.Count > 0)
            {
                merged[0] = newDict;
            }
            else
            {
                merged.Add(newDict);
            }

            foreach (Window window in app.Windows)
            {
                ApplyWindowChrome(window);
            }
        }

        if (persist && !string.IsNullOrWhiteSpace(_preferenceFilePath))
        {
            try
            {
                File.WriteAllText(_preferenceFilePath, theme.ToString());
            }
            catch
            {
                // Ignore non-critical UI preference write failures.
            }
        }

        ThemeChanged?.Invoke(null, theme);
    }

    public static void ApplyWindowChrome(Window window)
    {
        if (window is null)
        {
            return;
        }

        void UpdateTitleBar()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var useDark = CurrentTheme == AppTheme.Dark ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        }

        if (window.IsLoaded)
        {
            UpdateTitleBar();
        }
        else
        {
            window.SourceInitialized += (_, _) => UpdateTitleBar();
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
