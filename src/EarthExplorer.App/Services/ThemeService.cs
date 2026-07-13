using System.Windows;
using System.Windows.Media;

namespace EarthExplorer.App.Services;

public sealed class ThemeService : IThemeService
{
    public bool IsDark { get; private set; } = true;

    public void Toggle()
    {
        IsDark = !IsDark;
        var resources = Application.Current.Resources;
        Set(resources, "AppBackgroundBrush", IsDark ? "#0B111B" : "#EFF3F8");
        Set(resources, "PanelBrush", IsDark ? "#121A27" : "#FFFFFF");
        Set(resources, "PanelRaisedBrush", IsDark ? "#182232" : "#E8EEF6");
        Set(resources, "TextPrimaryBrush", IsDark ? "#F4F7FB" : "#132033");
        Set(resources, "TextSecondaryBrush", IsDark ? "#91A0B5" : "#5C6B7E");
        Set(resources, "BorderBrush", IsDark ? "#263347" : "#CAD4E1");
        Set(resources, "AccentMutedBrush", IsDark ? "#3D2030" : "#FFE4EA");
    }

    private static void Set(ResourceDictionary resources, string key, string color) =>
        resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
