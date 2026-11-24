using System.Windows;
using System.Windows.Input;

namespace Switchly.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Browser.AddressChanged += (_, e) => AddressBox.Text = (e.NewValue as string) ?? string.Empty;
    }

    private void OnGo(object sender, RoutedEventArgs e) => Navigate();

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Navigate();
        }
    }

    private void Navigate()
    {
        var url = AddressBox.Text.Trim();
        if (url.Length == 0)
        {
            return;
        }
        if (!url.Contains("://"))
        {
            url = "https://" + url;
        }
        Browser.Load(url);
    }
}
