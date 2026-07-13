using System.Text.RegularExpressions;
using EarthExplorer.Core.Models;
using EarthExplorer.Core.Services;
using TesseractOCR;
using TesseractOCR.Enums;

namespace EarthExplorer.AI.Services;

public sealed class LocalPhotoGeolocationService(
    IImageMetadataService imageMetadata,
    IGeocodingService geocoding) : IPhotoGeolocationService
{
    private const int MaximumCandidateQueries = 5;
    private static readonly char[] CandidateSeparators = ['\r', '\n', '|', ';'];

    public async Task<PhotoGeolocationResult> AnalyzeAsync(
        string filePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Читаю EXIF локально…");
        var metadata = await imageMetadata.ReadAsync(filePath, cancellationToken);
        if (metadata.GpsCoordinate is { } gps)
        {
            var gpsHypothesis = new PhotoGeolocationHypothesis(
                "Координаты из EXIF",
                gps,
                99,
                0,
                "GPS записан камерой в метаданные исходного файла.",
                "EXIF");

            return new PhotoGeolocationResult(
                metadata,
                string.Empty,
                [gpsHypothesis],
                "GPS найден в EXIF. Изображение и его текст не отправлялись в сеть.");
        }

        progress?.Report("Распознаю русский и английский текст локально…");
        var ocr = await Task.Run(
            () => RecognizeText(filePath, cancellationToken),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = ExtractCandidates(ocr.Text).Take(MaximumCandidateQueries).ToArray();
        if (candidates.Length == 0)
        {
            return new PhotoGeolocationResult(
                metadata,
                ocr.Text,
                [],
                "GPS отсутствует, а OCR не нашёл достаточно надёжного текста для поиска места.");
        }

        progress?.Report("Проверяю распознанные названия через поиск мест…");
        var hypotheses = new List<PhotoGeolocationHypothesis>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var results = await geocoding.SearchAsync(candidate, cancellationToken);
            var match = results.FirstOrDefault(result =>
                seen.Add($"{result.Coordinate.Latitude:F4}:{result.Coordinate.Longitude:F4}"));
            if (match is null)
            {
                continue;
            }

            var rank = hypotheses.Count;
            var confidence = Math.Clamp(
                (int)Math.Round(ocr.MeanConfidence * 55) + 25 - rank * 12,
                18,
                78);
            hypotheses.Add(new PhotoGeolocationHypothesis(
                match.DisplayName,
                match.Coordinate,
                confidence,
                rank switch
                {
                    0 => 25,
                    1 => 75,
                    _ => 150,
                },
                $"OCR распознал фрагмент: «{candidate}»",
                "Локальный OCR + Nominatim"));

            if (hypotheses.Count == 3)
            {
                break;
            }
        }

        var summary = hypotheses.Count == 0
            ? "Текст распознан, но поиск мест не подтвердил ни одной географической гипотезы."
            : $"Найдено гипотез: {hypotheses.Count}. Это ориентиры по тексту, а не доказательство точного места.";
        return new PhotoGeolocationResult(metadata, ocr.Text, hypotheses, summary);
    }

    private static OcrResult RecognizeText(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        var requiredFiles = new[] { "eng.traineddata", "rus.traineddata" };
        var missing = requiredFiles
            .Where(file => !File.Exists(Path.Combine(tessdataPath, file)))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Не найдены OCR-модели: {string.Join(", ", missing)}. Переустановите ExploreEarth полной версией.");
        }

        using var engine = new Engine(tessdataPath, "eng+rus", EngineMode.Default);
        using var image = TesseractOCR.Pix.Image.LoadFromFile(filePath);
        string text;
        double confidence;
        using (var page = engine.Process(image, PageSegMode.SparseText))
        {
            cancellationToken.ThrowIfCancellationRequested();
            text = page.Text?.Trim() ?? string.Empty;
            confidence = double.IsFinite(page.MeanConfidence)
                ? Math.Clamp(page.MeanConfidence, 0, 1)
                : 0;
        }

        engine.ClearPersistentCache();
        return new OcrResult(text, confidence);
    }

    private static IEnumerable<string> ExtractCandidates(string text)
    {
        return text
            .Split(CandidateSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeCandidate)
            .Where(candidate => candidate.Length is >= 3 and <= 64)
            .Where(candidate => candidate.Count(char.IsLetter) >= 3)
            .Where(candidate => candidate.Count(char.IsDigit) <= candidate.Length / 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(GeographicSignal)
            .ThenByDescending(candidate => candidate.Length);
    }

    private static string NormalizeCandidate(string value) =>
        Regex.Replace(value, @"[^\p{L}\p{M}\p{N}\s'.\-]", " ")
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim(' ', '.', '-', '\'');

    private static int GeographicSignal(string candidate)
    {
        var score = 0;
        if (candidate.Any(char.IsUpper))
        {
            score += 2;
        }

        if (candidate.Contains(' '))
        {
            score += 2;
        }

        if (candidate.Any(character => character is 'ё' or 'Ё'))
        {
            score++;
        }

        return score;
    }

    private sealed record OcrResult(string Text, double MeanConfidence);
}
