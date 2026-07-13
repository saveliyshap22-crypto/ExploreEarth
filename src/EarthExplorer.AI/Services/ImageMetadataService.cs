using EarthExplorer.Core.Models;
using EarthExplorer.Core.Services;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace EarthExplorer.AI.Services;

public sealed class ImageMetadataService : IImageMetadataService
{
    private const long MaximumFileSizeBytes = 80L * 1024L * 1024L;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff",
    };

    public Task<ImageMetadataResult> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Task.Run(() => Read(filePath, cancellationToken), cancellationToken);
    }

    private static ImageMetadataResult Read(string filePath, CancellationToken cancellationToken)
    {
        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Изображение не найдено.", filePath);
        }

        if (!SupportedExtensions.Contains(file.Extension))
        {
            throw new NotSupportedException("Поддерживаются JPG, PNG, WEBP, BMP и TIFF.");
        }

        if (file.Length > MaximumFileSizeBytes)
        {
            throw new InvalidDataException("Файл больше допустимого лимита 80 МБ.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var directories = ImageMetadataReader.ReadMetadata(file.FullName);
        cancellationToken.ThrowIfCancellationRequested();

        GeoCoordinate? coordinate = null;
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        var location = gps?.GetGeoLocation();
        if (location is not null && !location.IsZero)
        {
            coordinate = new GeoCoordinate(location.Latitude, location.Longitude);
        }

        DateTimeOffset? capturedAt = null;
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (subIfd is not null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var captured))
        {
            capturedAt = new DateTimeOffset(DateTime.SpecifyKind(captured, DateTimeKind.Local));
        }

        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var make = ifd0?.GetDescription(ExifDirectoryBase.TagMake)?.Trim();
        var model = ifd0?.GetDescription(ExifDirectoryBase.TagModel)?.Trim();
        var camera = string.Join(' ', new[] { make, model }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());

        double? altitude = null;
        if (gps?.TryGetDouble(GpsDirectory.TagAltitude, out var altitudeValue) == true)
        {
            altitude = altitudeValue;
        }

        var direction = gps?.GetDescription(GpsDirectory.TagImgDirection);
        return new ImageMetadataResult(
            file.Name,
            coordinate,
            capturedAt,
            string.IsNullOrWhiteSpace(camera) ? null : camera,
            altitude,
            direction);
    }
}
