using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Switchly.Domain.Groups;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>Modal dialog for creating or editing a BrowserProfile (UI-002, REQ-002, REQ-015).</summary>
public partial class ProfileEditorDialog : Window
{
    private sealed class GroupSelectionOption(Guid groupId, string label, bool isSelected)
    {
        public Guid GroupId { get; } = groupId;

        public string Label { get; } = label;

        public bool IsSelected { get; set; } = isSelected;
    }

    private readonly List<GroupSelectionOption> _groupOptions = [];

    public ProfileEditorDialog(
        BrowserProfile? existing = null,
        IReadOnlyList<ProfileGroup>? availableManualGroups = null,
        IReadOnlyCollection<Guid>? currentManualGroupIds = null)
    {
        InitializeComponent();

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
            ColorHexBox.Text = existing.ColorHex ?? "#3B82F6";
            FavoriteCheck.IsChecked = existing.IsFavorite;
            RestoreSessionCheck.IsChecked = existing.RestoreLastSession;
            PersistCookiesCheck.IsChecked = existing.PersistSessionCookies;
        }
        else
        {
            Title = "Profil Baru — Switchly";
            ColorHexBox.Text = "#3B82F6";
        }

        UpdateLivePreview();
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
        }
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e) => UpdateLivePreview();

    private void OnColorHexChanged(object sender, TextChangedEventArgs e) => UpdateLivePreview();

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

        try
        {
            var rawHex = ColorHexBox.Text.Trim();
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
