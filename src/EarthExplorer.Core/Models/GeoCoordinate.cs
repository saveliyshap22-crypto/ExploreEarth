using System.Globalization;

namespace EarthExplorer.Core.Models;

public readonly record struct GeoCoordinate
{
    public GeoCoordinate(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Широта должна быть от -90 до 90 градусов.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Долгота должна быть от -180 до 180 градусов.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Latitude:F6}, {Longitude:F6}");
}
