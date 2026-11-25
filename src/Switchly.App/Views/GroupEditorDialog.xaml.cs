using System.Windows;
using System.Windows.Controls;
using Switchly.Domain.Groups;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>Modal dialog for creating a Manual / Web Workspace Group with 1-click presets (D10).</summary>
public partial class GroupEditorDialog : Window
{
    public GroupEditorDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    public string GroupName { get; private set; } = string.Empty;

    public string? DefaultUrl { get; private set; }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string payload)
        {
            var parts = payload.Split('|', 2);
            if (parts.Length == 2)
            {
                NameBox.Text = parts[0];
                DefaultUrlBox.Text = parts[1];
                ErrorText.Text = string.Empty;
            }
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            GroupName = GroupValidation.NormalizeAndValidateName(NameBox.Text);
            DefaultUrl = ProfileValidation.NormalizeAndValidateStartupUrl(DefaultUrlBox.Text);
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
