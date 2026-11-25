using System.Windows;

namespace Switchly.App.Themes;

public static class TextBoxAssist
{
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderText",
            typeof(string),
            typeof(TextBoxAssist),
            new PropertyMetadata(string.Empty));

    public static string GetPlaceholderText(DependencyObject obj) =>
        (string)obj.GetValue(PlaceholderTextProperty);

    public static void SetPlaceholderText(DependencyObject obj, string value) =>
        obj.SetValue(PlaceholderTextProperty, value);
}
