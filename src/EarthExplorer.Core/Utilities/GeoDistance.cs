using EarthExplorer.Core.Models;

namespace EarthExplorer.Core.Utilities;

public static class GeoDistance
{
    private const double EarthRadiusKilometers = 6371.0088;

    public static double BetweenKilometers(GeoCoordinate first, GeoCoordinate second)
    {
        var latitudeDelta = DegreesToRadians(second.Latitude - first.Latitude);
        var longitudeDelta = DegreesToRadians(second.Longitude - first.Longitude);
        var firstLatitude = DegreesToRadians(first.Latitude);
        var secondLatitude = DegreesToRadians(second.Latitude);

        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return 2 * EarthRadiusKilometers * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    public static int GeoGuessScore(double distanceKilometers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distanceKilometers);
        return (int)Math.Round(5000 * Math.Exp(-distanceKilometers / 2000));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
