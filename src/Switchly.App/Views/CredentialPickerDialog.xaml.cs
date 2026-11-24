using System.Windows;
using System.Windows.Input;
using Switchly.Application.Vault;

namespace Switchly.App.Views;

/// <summary>Compact picker shown when multiple vault credentials match the current origin.</summary>
public partial class CredentialPickerDialog : Window
{
    public CredentialPickerDialog(IReadOnlyList<VaultCredentialMatch> matches)
    {
        InitializeComponent();
        CredentialsList.ItemsSource = matches;
        CredentialsList.SelectedIndex = 0;
    }

    public VaultCredentialMatch? SelectedMatch => CredentialsList.SelectedItem as VaultCredentialMatch;

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        if (SelectedMatch is null)
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedMatch is not null)
        {
            DialogResult = true;
            Close();
        }
    }
}
