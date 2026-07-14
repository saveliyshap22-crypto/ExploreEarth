using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using EarthExplorer.Core.Models;
using EarthExplorer.Core.Utilities;

namespace EarthExplorer.App.Services;

public sealed record PanoramaxPicture(
    string Id,
    GeoCoordinate Coordinate,
    Uri? ImageUri,
    Uri ViewerUri,
    double DistanceKilometers);

public sealed class PanoramaxService(HttpClient httpClient)
{
    private static readonly Uri[] Instances =
    [
        new("https://api.panoramax.xyz/"),
        new("https://panoramax.openstreetmap.fr/"),
        new("https://panoramax.ign.fr/"),
    ];

    private static readonly double[] SearchRadiiDegrees = [0.0025, 0.01, 0.05, 0.2];

    public async Task<PanoramaxPicture?> FindNearestAsync(
        GeoCoordinate coordinate,
        CancellationToken cancellationToken)
    {
        foreach (var radius in SearchRadiiDegrees)
        {
            PanoramaxPicture? nearest = null;

            foreach (var instance in Instances)
            {
                try
                {
                    var candidate = await SearchInstanceAsync(instance, coordinate, radius, cancellationToken);
                    if (candidate is not null &&
                        (nearest is null || candidate.DistanceKilometers < nearest.DistanceKilometers))
                    {
                        nearest = candidate;
                    }
                }
                catch (HttpRequestException)
                {
                    // A federated instance may be temporarily unavailable. Try the next one.
                }
                catch (JsonException)
                {
                    // Ignore malformed responses from one instance and continue with the others.
                }
            }

            if (nearest is not null)
            {
                return nearest;
            }
        }

        return null;
    }

    public static Uri BuildCoverageUri(GeoCoordinate coordinate)
    {
        var latitude = coordinate.Latitude.ToString("F6", CultureInfo.InvariantCulture);
        var longitude = coordinate.Longitude.ToString("F6", CultureInfo.InvariantCulture);
        return new Uri($"https://api.panoramax.xyz/?focus=map&map=18/{latitude}/{longitude}&users=metacatalog");
    }

    private async Task<PanoramaxPicture?> SearchInstanceAsync(
        Uri instance,
        GeoCoordinate target,
        double radius,
        CancellationToken cancellationToken)
    {
        var minLongitude = Math.Max(-180, target.Longitude - radius);
        var minLatitude = Math.Max(-90, target.Latitude - radius);
        var maxLongitude = Math.Min(180, target.Longitude + radius);
        var maxLatitude = Math.Min(90, target.Latitude + radius);
        var bbox = string.Join(",",
            minLongitude.ToString("F7", CultureInfo.InvariantCulture),
            minLatitude.ToString("F7", CultureInfo.InvariantCulture),
            maxLongitude.ToString("F7", CultureInfo.InvariantCulture),
            maxLatitude.ToString("F7", CultureInfo.InvariantCulture));

        var searchUri = new Uri(instance, $"api/search?bbox={bbox}&limit=50");
        using var response = await httpClient.GetAsync(searchUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        PanoramaxPicture? nearest = null;
        foreach (var feature in features.EnumerateArray())
        {
            if (!TryReadCoordinate(feature, out var coordinate) ||
                !feature.TryGetProperty("id", out var idElement))
            {
                continue;
            }

            var id = idElement.GetString();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var distance = GeoDistance.BetweenKilometers(target, coordinate);
            var imageUri = TryReadImageUri(feature, instance);
            var latitude = coordinate.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            var longitude = coordinate.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            var viewerUri = new Uri(instance,
                $"?focus=pic&nav=seq&pic={Uri.EscapeDataString(id)}&map=18/{latitude}/{longitude}");
            var picture = new PanoramaxPicture(id, coordinate, imageUri, viewerUri, distance);

            if (nearest is null || picture.DistanceKilometers < nearest.DistanceKilometers)
            {
                nearest = picture;
            }
        }

        return nearest;
    }

    private static bool TryReadCoordinate(JsonElement feature, out GeoCoordinate coordinate)
    {
        coordinate = default;
        if (!feature.TryGetProperty("geometry", out var geometry) ||
            !geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Array ||
            coordinates.GetArrayLength() < 2)
        {
            return false;
        }

        var longitude = coordinates[0].GetDouble();
        var latitude = coordinates[1].GetDouble();
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            return false;
        }

        coordinate = new GeoCoordinate(latitude, longitude);
        return true;
    }

    private static Uri? TryReadImageUri(JsonElement feature, Uri instance)
    {
        if (!feature.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var preferredKey in new[] { "hd", "sd", "thumb", "thumbnail", "original" })
        {
            if (assets.TryGetProperty(preferredKey, out var asset) && TryReadAssetUri(asset, instance, out var uri))
            {
                return uri;
            }
        }

        foreach (var assetProperty in assets.EnumerateObject())
        {
            if (TryReadAssetUri(assetProperty.Value, instance, out var uri))
            {
                return uri;
            }
        }

        return null;
    }

    private static bool TryReadAssetUri(JsonElement asset, Uri instance, out Uri? uri)
    {
        uri = null;
        if (asset.ValueKind == JsonValueKind.String)
        {
            return Uri.TryCreate(instance, asset.GetString(), out uri);
        }

        if (asset.ValueKind != JsonValueKind.Object ||
            !asset.TryGetProperty("href", out var hrefElement))
        {
            return false;
        }

        var href = hrefElement.GetString();
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        if (asset.TryGetProperty("type", out var typeElement))
        {
            var mediaType = typeElement.GetString();
            if (!string.IsNullOrWhiteSpace(mediaType) &&
                !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return Uri.TryCreate(instance, href, out uri);
    }
}
