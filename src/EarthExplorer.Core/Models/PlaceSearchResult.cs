namespace EarthExplorer.Core.Models;

public sealed record PlaceSearchResult(
    string DisplayName,
    string Category,
    GeoCoordinate Coordinate,
    string? CountryCode = null)
{
    public string CoordinateText => Coordinate.ToString();
}
