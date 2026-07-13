using System.Globalization;
using System.Text.RegularExpressions;
using EarthExplorer.Core.Models;

namespace EarthExplorer.Core.Utilities;

public static partial class CoordinateParser
{
    private static readonly char[] Separators = [',', ';'];

    public static CoordinateParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new(false, null, "Введите координаты.");
        }

        var normalized = input.Trim().Replace('−', '-');
        var decimalResult = TryParseDecimal(normalized);
        if (decimalResult.Success)
        {
            return decimalResult;
        }

        var dmsMatches = DmsRegex().Matches(normalized);
        if (dmsMatches.Count == 2 &&
            TryConvertDms(dmsMatches[0], out var first) &&
            TryConvertDms(dmsMatches[1], out var second))
        {
            var firstHemisphere = dmsMatches[0].Groups["hemisphere"].Value.ToUpperInvariant();
            var latitudeFirst = firstHemisphere is "N" or "S" or "С" or "Ю";
            var latitude = latitudeFirst ? first : second;
            var longitude = latitudeFirst ? second : first;
            return Validate(latitude, longitude);
        }

        return new(false, null, "Не удалось распознать координаты. Пример: 59.9343, 30.3351.");
    }

    private static CoordinateParseResult TryParseDecimal(string input)
    {
        string[] parts;
        if (input.IndexOfAny(Separators) >= 0)
        {
            parts = input.Split(Separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            parts = WhitespaceRegex().Split(input.Trim());
        }

        if (parts.Length != 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var first) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var second))
        {
            return new(false, null, null);
        }

        return Validate(first, second);
    }

    private static CoordinateParseResult Validate(double latitude, double longitude)
    {
        if (latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
        {
            return new(true, new GeoCoordinate(latitude, longitude), null);
        }

        if (longitude is >= -90 and <= 90 && latitude is >= -180 and <= 180)
        {
            return new(
                true,
                new GeoCoordinate(longitude, latitude),
                "Похоже, широта и долгота были переставлены местами.",
                true);
        }

        return new(false, null, "Широта должна быть от -90 до 90, долгота — от -180 до 180.");
    }

    private static bool TryConvertDms(Match match, out double value)
    {
        value = 0;
        if (!double.TryParse(match.Groups["degrees"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) ||
            !double.TryParse(match.Groups["minutes"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        var seconds = 0d;
        if (match.Groups["seconds"].Success &&
            !double.TryParse(match.Groups["seconds"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
        {
            return false;
        }

        value = degrees + (minutes / 60d) + (seconds / 3600d);
        var hemisphere = match.Groups["hemisphere"].Value.ToUpperInvariant();
        if (hemisphere is "S" or "W" or "Ю" or "З")
        {
            value = -value;
        }

        return true;
    }

    [GeneratedRegex(@"(?<degrees>\d{1,3})(?:°|\s+)\s*(?<minutes>\d{1,2}(?:\.\d+)?)(?:['′]|\s+)\s*(?:(?<seconds>\d{1,2}(?:\.\d+)?)\s*(?:[""″]|\s+))?\s*(?<hemisphere>[NSEWСЮВЗ])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DmsRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
