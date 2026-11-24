using System.Windows;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>Modal dialog for creating or editing a BrowserProfile (UI-002, REQ-002, REQ-015).</summary>
public partial class ProfileEditorDialog : Window
{
    public ProfileEditorDialog(BrowserProfile? existing = null)
    {
        InitializeComponent();

        if (existing is not null)
        {
            Title = "Ubah Profil";
            NameBox.Text = existing.Name;
            StartupUrlBox.Text = existing.StartupUrl ?? string.Empty;
            ColorHexBox.Text = existing.ColorHex ?? "#1B6EF3";
            FavoriteCheck.IsChecked = existing.IsFavorite;
            RestoreSessionCheck.IsChecked = existing.RestoreLastSession;
            PersistCookiesCheck.IsChecked = existing.PersistSessionCookies;
        }
        else
        {
            Title = "Profil Baru";
            ColorHexBox.Text = "#1B6EF3";
        }
    }

    public string ProfileName { get; private set; } = string.Empty;

    public string? StartupUrl { get; private set; }

    public string? ColorHex { get; private set; }

    public bool IsFavorite { get; private set; }

    public bool RestoreLastSession { get; private set; }

    public bool PersistSessionCookies { get; private set; }

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
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
