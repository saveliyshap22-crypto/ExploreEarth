namespace EarthExplorer.App.Services;

public interface IThemeService
{
    bool IsDark { get; }

    void Toggle();
}
