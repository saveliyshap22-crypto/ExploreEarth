using EarthExplorer.Core.Utilities;
using Xunit;

namespace EarthExplorer.Tests;

public sealed class CoordinateParserTests
{
    [Theory]
    [InlineData("48.8584, 2.2945", 48.8584, 2.2945)]
    [InlineData("59.9343 30.3351", 59.9343, 30.3351)]
    [InlineData("59° 56' 3.48\" N 30° 20' 6.36\" E", 59.9343, 30.3351)]
    public void Parse_ValidCoordinates_ReturnsCoordinate(string input, double latitude, double longitude)
    {
        var result = CoordinateParser.Parse(input);

        Assert.True(result.Success);
        Assert.NotNull(result.Coordinate);
        Assert.Equal(latitude, result.Coordinate.Value.Latitude, 4);
        Assert.Equal(longitude, result.Coordinate.Value.Longitude, 4);
    }

    [Fact]
    public void Parse_SwappedCoordinates_SuggestsSwap()
    {
        var result = CoordinateParser.Parse("120, 45");

        Assert.True(result.Success);
        Assert.True(result.WasSwapped);
        Assert.Equal(45, result.Coordinate?.Latitude);
        Assert.Equal(120, result.Coordinate?.Longitude);
    }

    [Theory]
    [InlineData("91, 181")]
    [InlineData("")]
    [InlineData("не координаты")]
    public void Parse_InvalidCoordinates_ReturnsError(string input)
    {
        var result = CoordinateParser.Parse(input);

        Assert.False(result.Success);
        Assert.Null(result.Coordinate);
    }
}
