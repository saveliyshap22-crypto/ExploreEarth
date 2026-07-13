namespace EarthExplorer.Core.Models;

public sealed record PhotoGeolocationHypothesis(
    string DisplayName,
    GeoCoordinate Coordinate,
    int ConfidencePercent,
    int RadiusKilometers,
    string Evidence,
    string Source)
{
    public string CoordinateText => Coordinate.ToString();

    public string ConfidenceText => $"Уверенность: {ConfidencePercent}%";

    public string RadiusText => RadiusKilometers == 0
        ? "Точная точка из метаданных"
        : $"Ориентировочный радиус: {RadiusKilometers} км";
}
