using EarthExplorer.Core.Models;
using EarthExplorer.Core.Utilities;

namespace EarthExplorer.Tests;

public sealed class GeoDistanceTests
{
    [Fact]
    public void BetweenKilometers_ReturnsKnownParisLondonDistance()
    {
        var paris = new GeoCoordinate(48.8566, 2.3522);
        var london = new GeoCoordinate(51.5074, -0.1278);

        var distance = GeoDistance.BetweenKilometers(paris, london);

        Assert.InRange(distance, 340, 350);
    }

    [Theory]
    [InlineData(0, 5000)]
    [InlineData(2000, 1839)]
    public void GeoGuessScore_DecreasesWithDistance(double distance, int expected)
    {
        Assert.Equal(expected, GeoDistance.GeoGuessScore(distance));
    }
}
