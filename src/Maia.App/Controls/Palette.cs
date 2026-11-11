namespace Maia.App.Controls;

/// <summary>Colour tokens from <c>Resources/Styles/Colors.xaml</c>, for code that draws. Never hard-code hex.</summary>
public static class Palette
{
    public static Color Get(string key) =>
        Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Transparent;
}
