namespace EarthExplorer.Core.Models;

public sealed record GeoGuessRound(
    string PlaceName,
    string Clue,
    GeoCoordinate Coordinate);
