using EarthExplorer.Core.Models;

namespace EarthExplorer.Core.Services;

public interface IPhotoGeolocationService
{
    Task<PhotoGeolocationResult> AnalyzeAsync(
        string filePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
