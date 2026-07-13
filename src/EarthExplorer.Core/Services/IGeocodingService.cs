using EarthExplorer.Core.Models;

namespace EarthExplorer.Core.Services;

public interface IGeocodingService
{
    Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
