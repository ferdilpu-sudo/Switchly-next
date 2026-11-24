using System.Windows;

namespace Switchly.App.Views;

/// <summary>Minimal pure-WPF text prompt dialog (e.g., creating a manual profile group).</summary>
public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        Loaded += (_, _) => InputBox.Focus();
    }

    public string InputText => InputBox.Text.Trim();

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            return;
        }

        DialogResult = true;
    }
}
