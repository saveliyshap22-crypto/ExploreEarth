using EarthExplorer.Core.Models;

namespace EarthExplorer.Core.Services;

public interface IMapNavigationService
{
    void NavigateTo(GeoCoordinate coordinate, string label);

    void SetMapStyle(MapStyle style);

    void SetGeoGuess(GeoCoordinate coordinate);

    void RevealGeoGuess(GeoCoordinate coordinate);

    void ClearGeoGuess();

    void ShowWorld();

    void ZoomIn();

    void ZoomOut();

    void SetDarkMode(bool enabled);
}
