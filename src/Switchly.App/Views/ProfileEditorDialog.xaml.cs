using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Switchly.Domain.Groups;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>Modal dialog for creating or editing a BrowserProfile (UI-002, REQ-002, REQ-015).</summary>
public partial class ProfileEditorDialog : Window
{
    private const double WheelSizeDip = 132.0;
    private const double WheelThumbSizeDip = 16.0;

    private sealed class GroupSelectionOption(Guid groupId, string label, bool isSelected)
    {
        public Guid GroupId { get; } = groupId;

        public string Label { get; } = label;

        public bool IsSelected { get; set; } = isSelected;
    }

    private static readonly string[] DefaultAvatarPalette =
    [
        "#2563EB",
        "#7C3AED",
        "#059669",
        "#D97706",
        "#E11D48",
        "#0891B2",
        "#4F46E5",
        "#EA580C",
    ];

    private static BitmapSource? s_cachedWheelBitmap;

    private readonly List<GroupSelectionOption> _groupOptions = [];
    private double _hueDegrees = 221.0;
    private double _saturation = 0.84;
    private double _brightness = 0.92;
    private bool _isDraggingWheel;
    private bool _syncingColorControls;

    public ProfileEditorDialog(
        BrowserProfile? existing = null,
        IReadOnlyList<ProfileGroup>? availableManualGroups = null,
        IReadOnlyCollection<Guid>? currentManualGroupIds = null,
        IReadOnlyCollection<string>? existingProfileColors = null)
    {
        InitializeComponent();

        s_cachedWheelBitmap ??= BuildAntialiasedHsvWheelBitmap(pixelDiameter: 264);
        ColorWheelImage.Source = s_cachedWheelBitmap;
        MiniWheelIcon.Source = s_cachedWheelBitmap;

        // Adapt scrollable body max height to current monitor DPI / WorkArea so Simpan/Batal is NEVER clipped.
        BodyScrollViewer.MaxHeight = Math.Clamp(SystemParameters.WorkArea.Height - 190, 260, 480);

        var selectedSet = currentManualGroupIds?.ToHashSet() ?? [];
        if (availableManualGroups is not null)
        {
            foreach (var group in availableManualGroups)
            {
                _groupOptions.Add(new GroupSelectionOption(
                    group.GroupId,
                    group.Name,
                    selectedSet.Contains(group.GroupId)));
            }
        }

        GroupsItemsControl.ItemsSource = _groupOptions;
        NoGroupsHint.Visibility = _groupOptions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (existing is not null)
        {
            Title = "Ubah Profil — Switchly";
            NameBox.Text = existing.Name;
            StartupUrlBox.Text = existing.StartupUrl ?? string.Empty;
            var existingHex = existing.ColorHex ?? "#2563EB";
            ColorHexBox.Text = existingHex;
            SyncWheelFromHex(existingHex);
            FavoriteCheck.IsChecked = existing.IsFavorite;
            RestoreSessionCheck.IsChecked = existing.RestoreLastSession;
            PersistCookiesCheck.IsChecked = existing.PersistSessionCookies;

            if (!DefaultAvatarPalette.Contains(existingHex, StringComparer.OrdinalIgnoreCase))
            {
                ColorWheelPanel.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrWhiteSpace(existing.StartupUrl) ||
                existing.IsFavorite ||
                !existing.RestoreLastSession ||
                !existing.PersistSessionCookies)
            {
                SetAdvancedExpanded(true, scrollIntoView: false);
            }
        }
        else
        {
            Title = "Profil Baru — Switchly";
            var picked = PickDistinctPaletteColor(existingProfileColors);
            ColorHexBox.Text = picked;
            SyncWheelFromHex(picked);
        }

        UpdateLivePreview();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private static string PickDistinctPaletteColor(IReadOnlyCollection<string>? existingColors)
    {
        if (existingColors is null || existingColors.Count == 0)
        {
            return DefaultAvatarPalette[0];
        }

        var counts = DefaultAvatarPalette.ToDictionary(c => c, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var raw in existingColors)
        {
            if (!string.IsNullOrWhiteSpace(raw) && counts.TryGetValue(raw.Trim(), out var current))
            {
                counts[raw.Trim()] = current + 1;
            }
        }

        return DefaultAvatarPalette
            .OrderBy(c => counts[c])
            .First();
    }

    private void OnToggleColorWheelClick(object sender, RoutedEventArgs e)
    {
        var show = ColorWheelPanel.Visibility != Visibility.Visible;
        ColorWheelPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            SyncWheelFromHex(ColorHexBox.Text);
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ColorWheelPanel.BringIntoView());
        }
    }

    private void OnToggleAdvancedClick(object sender, RoutedEventArgs e)
        => SetAdvancedExpanded(AdvancedOptionsPanel.Visibility != Visibility.Visible, scrollIntoView: true);

    private void SetAdvancedExpanded(bool expanded, bool scrollIntoView)
    {
        AdvancedOptionsPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        var iconKey = expanded ? "IconChevronDown" : "IconChevronRight";
        if (System.Windows.Application.Current?.TryFindResource(iconKey) is Geometry g)
        {
            AdvancedChevron.Data = g;
        }

        if (expanded && scrollIntoView)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AdvancedOptionsPanel.BringIntoView());
        }
    }

    public string ProfileName { get; private set; } = string.Empty;

    public string? StartupUrl { get; private set; }

    public string? ColorHex { get; private set; }

    public bool IsFavorite { get; private set; }

    public bool RestoreLastSession { get; private set; }

    public bool PersistSessionCookies { get; private set; }

    public IReadOnlyList<Guid> SelectedManualGroupIds { get; private set; } = [];

    private void OnColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string hex)
        {
            ColorHexBox.Text = hex;
            SyncWheelFromHex(hex);
        }
    }

    private void OnColorWheelMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingWheel = true;
        ColorWheelCanvas.CaptureMouse();
        UpdateColorFromWheelPoint(e.GetPosition(ColorWheelCanvas));
        e.Handled = true;
    }

    private void OnColorWheelMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingWheel || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateColorFromWheelPoint(e.GetPosition(ColorWheelCanvas));
    }

    private void OnColorWheelMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingWheel)
        {
            _isDraggingWheel = false;
            ColorWheelCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnColorBrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingColorControls || ColorHexBox is null)
        {
            return;
        }

        _brightness = e.NewValue;
        ApplyHsvToControls();
    }

    private void UpdateColorFromWheelPoint(Point pt)
    {
        var radius = WheelSizeDip / 2.0;
        var dx = pt.X - radius;
        var dy = pt.Y - radius;
        var dist = Math.Sqrt((dx * dx) + (dy * dy));
        var angleRad = Math.Atan2(dy, dx);
        var deg = angleRad * (180.0 / Math.PI);
        if (deg < 0)
        {
            deg += 360.0;
        }

        _hueDegrees = deg;
        _saturation = Math.Clamp(dist / radius, 0.0, 1.0);
        ApplyHsvToControls();
    }

    private void ApplyHsvToControls()
    {
        var color = HsvToRgb(_hueDegrees, _saturation, _brightness);
        PositionWheelThumb(color);

        _syncingColorControls = true;
        try
        {
            ColorHexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        finally
        {
            _syncingColorControls = false;
        }

        UpdateLivePreview();
    }

    private void SyncWheelFromHex(string? rawHex)
    {
        if (string.IsNullOrWhiteSpace(rawHex))
        {
            return;
        }

        var trimmed = rawHex.Trim();
        if (trimmed.Length != 7 || !trimmed.StartsWith('#'))
        {
            return;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(trimmed);
            RgbToHsv(color, out _hueDegrees, out _saturation, out _brightness);
            _syncingColorControls = true;
            try
            {
                if (ColorBrightnessSlider is not null)
                {
                    ColorBrightnessSlider.Value = Math.Clamp(_brightness, ColorBrightnessSlider.Minimum, 1.0);
                }
            }
            finally
            {
                _syncingColorControls = false;
            }

            PositionWheelThumb(color);
        }
        catch
        {
        }
    }

    private void PositionWheelThumb(Color currentColor)
    {
        if (ColorWheelThumb is null)
        {
            return;
        }

        var radius = WheelSizeDip / 2.0;
        var effectiveRadius = _saturation * (radius - 3.0);
        var rad = _hueDegrees * (Math.PI / 180.0);
        var cx = radius + (effectiveRadius * Math.Cos(rad));
        var cy = radius + (effectiveRadius * Math.Sin(rad));

        Canvas.SetLeft(ColorWheelThumb, cx - (WheelThumbSizeDip / 2.0));
        Canvas.SetTop(ColorWheelThumb, cy - (WheelThumbSizeDip / 2.0));
        ColorWheelThumb.Background = new SolidColorBrush(currentColor);
    }

    private static BitmapSource BuildAntialiasedHsvWheelBitmap(int pixelDiameter)
    {
        var wb = new WriteableBitmap(pixelDiameter, pixelDiameter, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[pixelDiameter * pixelDiameter * 4];
        var center = (pixelDiameter - 1) / 2.0;
        var outerR = (pixelDiameter / 2.0) - 1.0;

        for (var y = 0; y < pixelDiameter; y++)
        {
            for (var x = 0; x < pixelDiameter; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var dist = Math.Sqrt((dx * dx) + (dy * dy));
                var offset = ((y * pixelDiameter) + x) * 4;

                if (dist > outerR + 1.0)
                {
                    continue;
                }

                var alpha = dist <= outerR - 0.8 ? 1.0 : Math.Clamp(outerR + 0.8 - dist, 0.0, 1.0);
                var deg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                if (deg < 0)
                {
                    deg += 360.0;
                }

                var sat = Math.Clamp(dist / outerR, 0.0, 1.0);
                var c = HsvToRgb(deg, sat, 0.96);
                var aByte = (byte)Math.Round(alpha * 255.0);
                // Bgra32 in WPF uses premultiplied alpha
                pixels[offset + 0] = (byte)((c.B * aByte) / 255);
                pixels[offset + 1] = (byte)((c.G * aByte) / 255);
                pixels[offset + 2] = (byte)((c.R * aByte) / 255);
                pixels[offset + 3] = aByte;
            }
        }

        wb.WritePixels(new Int32Rect(0, 0, pixelDiameter, pixelDiameter), pixels, pixelDiameter * 4, 0);
        wb.Freeze();
        return wb;
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        var c = v * s;
        var x = c * (1.0 - Math.Abs(((h / 60.0) % 2.0) - 1.0));
        var m = v - c;

        var (r1, g1, b1) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(
            (byte)Math.Round(Math.Clamp((r1 + m) * 255.0, 0, 255)),
            (byte)Math.Round(Math.Clamp((g1 + m) * 255.0, 0, 255)),
            (byte)Math.Round(Math.Clamp((b1 + m) * 255.0, 0, 255)));
    }

    private static void RgbToHsv(Color rgb, out double h, out double s, out double v)
    {
        var r = rgb.R / 255.0;
        var g = rgb.G / 255.0;
        var b = rgb.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        if (delta < 0.00001)
        {
            h = 0;
        }
        else if (Math.Abs(max - r) < 0.00001)
        {
            h = 60.0 * (((g - b) / delta) % 6.0);
        }
        else if (Math.Abs(max - g) < 0.00001)
        {
            h = 60.0 * (((b - r) / delta) + 2.0);
        }
        else
        {
            h = 60.0 * (((r - g) / delta) + 4.0);
        }

        if (h < 0)
        {
            h += 360.0;
        }

        s = max <= 0 ? 0 : delta / max;
        v = max;
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e) => UpdateLivePreview();

    private void OnColorHexChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncingColorControls)
        {
            SyncWheelFromHex(ColorHexBox.Text);
        }

        UpdateLivePreview();
    }

    private void UpdateLivePreview()
    {
        if (PreviewTitleText is null || AvatarPreviewLetter is null || AvatarPreviewBorder is null)
        {
            return;
        }

        var trimmed = NameBox.Text.Trim();
        PreviewTitleText.Text = string.IsNullOrWhiteSpace(trimmed) ? "Profil Baru" : trimmed;
        AvatarPreviewLetter.Text = string.IsNullOrWhiteSpace(trimmed)
            ? "P"
            : trimmed[0].ToString().ToUpperInvariant();

        var rawHex = ColorHexBox.Text.Trim();
        try
        {
            if (rawHex.Length == 7 && rawHex.StartsWith('#'))
            {
                var color = (Color)ColorConverter.ConvertFromString(rawHex);
                AvatarPreviewBorder.Background = new SolidColorBrush(color);
            }
        }
        catch
        {
            // Ignore transient invalid hex while user is typing.
        }

        var anyPresetMatch = false;
        if (SwatchPanel is not null)
        {
            foreach (var child in SwatchPanel.Children)
            {
                if (child is Button btn && btn.Tag is string swatchHex)
                {
                    var isMatch = string.Equals(swatchHex, rawHex, StringComparison.OrdinalIgnoreCase);
                    if (isMatch)
                    {
                        anyPresetMatch = true;
                    }

                    btn.Content = isMatch
                        ? new TextBlock
                        {
                            Text = "✓",
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.Bold,
                            FontSize = 13,
                        }
                        : null;
                    if (isMatch)
                    {
                        btn.SetResourceReference(BorderBrushProperty, "TextPrimaryBrush");
                    }
                    else
                    {
                        btn.BorderBrush = Brushes.Transparent;
                    }
                }
            }
        }

        if (CustomColorToggleButton is not null)
        {
            CustomColorToggleButton.SetResourceReference(
                BorderBrushProperty,
                !anyPresetMatch && rawHex.Length == 7 && rawHex.StartsWith('#') ? "AccentBrush" : "BorderStrongBrush");
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ProfileName = ProfileValidation.NormalizeAndValidateName(NameBox.Text);
            StartupUrl = ProfileValidation.NormalizeAndValidateStartupUrl(StartupUrlBox.Text);
            ColorHex = ProfileValidation.NormalizeAndValidateColorHex(ColorHexBox.Text);
            IsFavorite = FavoriteCheck.IsChecked == true;
            RestoreLastSession = RestoreSessionCheck.IsChecked == true;
            PersistSessionCookies = PersistCookiesCheck.IsChecked == true;
            SelectedManualGroupIds = _groupOptions
                .Where(o => o.IsSelected)
                .Select(o => o.GroupId)
                .ToList();
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
