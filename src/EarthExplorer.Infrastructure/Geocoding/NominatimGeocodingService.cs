using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EarthExplorer.Core.Models;
using EarthExplorer.Core.Services;

namespace EarthExplorer.Infrastructure.Geocoding;

public sealed partial class NominatimGeocodingService(HttpClient httpClient) : IGeocodingService
{
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<string, IReadOnlyList<PlaceSearchResult>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var normalized = query.Trim();
        if (_cache.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        await _rateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var remainingDelay = MinimumRequestInterval - (DateTimeOffset.UtcNow - _lastRequestAt);
            if (remainingDelay > TimeSpan.Zero)
            {
                await Task.Delay(remainingDelay, cancellationToken).ConfigureAwait(false);
            }

            var url = $"search?format=jsonv2&limit=6&addressdetails=1&accept-language=ru&q={Uri.EscapeDataString(normalized)}";
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            _lastRequestAt = DateTimeOffset.UtcNow;
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync(
                stream,
                NominatimJsonContext.Default.ListNominatimItem,
                cancellationToken).ConfigureAwait(false) ?? [];

            var results = payload
                .Select(Map)
                .Where(result => result is not null)
                .Cast<PlaceSearchResult>()
                .ToArray();
            _cache.TryAdd(normalized, results);
            return results;
        }
        finally
        {
            _rateGate.Release();
        }
    }

    private static PlaceSearchResult? Map(NominatimItem item)
    {
        if (!double.TryParse(item.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(item.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        return new PlaceSearchResult(
            item.DisplayName,
            string.IsNullOrWhiteSpace(item.Type) ? "место" : item.Type,
            new GeoCoordinate(latitude, longitude),
            item.Address?.CountryCode);
    }

    private sealed record NominatimItem(
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("lat")] string Latitude,
        [property: JsonPropertyName("lon")] string Longitude,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("address")] NominatimAddress? Address);

    private sealed record NominatimAddress(
        [property: JsonPropertyName("country_code")] string? CountryCode);

    [JsonSerializable(typeof(List<NominatimItem>))]
    private sealed partial class NominatimJsonContext : JsonSerializerContext;
}
