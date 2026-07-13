using EarthExplorer.Core.Models;

namespace EarthExplorer.Core.Services;

public interface IImageMetadataService
{
    Task<ImageMetadataResult> ReadAsync(string filePath, CancellationToken cancellationToken);
}
