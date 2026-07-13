namespace EarthExplorer.Core.Models;

public sealed record ImageMetadataResult(
    string FileName,
    GeoCoordinate? GpsCoordinate,
    DateTimeOffset? CapturedAt,
    string? Camera,
    double? AltitudeMeters,
    string? Direction)
{
    public bool HasGps => GpsCoordinate is not null;
}
