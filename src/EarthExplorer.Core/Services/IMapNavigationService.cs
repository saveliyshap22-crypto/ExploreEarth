using EarthExplorer.Core.Models;

namespace EarthExplorer.Core.Services;

public interface IMapNavigationService
{
    void NavigateTo(GeoCoordinate coordinate, string label);

    void ShowWorld();

    void ZoomIn();

    void ZoomOut();

    void SetDarkMode(bool enabled);
}
