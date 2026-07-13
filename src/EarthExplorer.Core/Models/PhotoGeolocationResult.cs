namespace EarthExplorer.Core.Models;

public sealed record PhotoGeolocationResult(
    ImageMetadataResult Metadata,
    string RecognizedText,
    IReadOnlyList<PhotoGeolocationHypothesis> Hypotheses,
    string Summary);
