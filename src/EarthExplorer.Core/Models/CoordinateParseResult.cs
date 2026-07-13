namespace EarthExplorer.Core.Models;

public sealed record CoordinateParseResult(
    bool Success,
    GeoCoordinate? Coordinate,
    string? Error,
    bool WasSwapped = false);
