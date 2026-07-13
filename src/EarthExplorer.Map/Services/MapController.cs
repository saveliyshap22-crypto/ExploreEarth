using BruTile.Predefined;
using BruTile.Web;
using EarthExplorer.Core.Models;
using EarthExplorer.Core.Services;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.Widgets.ScaleBar;
using EarthMapStyle = EarthExplorer.Core.Models.MapStyle;

namespace EarthExplorer.Map.Services;

public sealed class MapController : IMapNavigationService, IDisposable
{
    private const string ApplicationUserAgent =
        "ExploreEarth/0.2 (+https://github.com/saveliyshap22-crypto/ExploreEarth)";
    private TileLayer _baseLayer;
    private readonly MemoryLayer _resultLayer;
    private readonly MemoryLayer _guessLayer;
    private readonly MemoryLayer _targetLayer;

    public MapController()
    {
        Map = new Mapsui.Map
        {
            CRS = "EPSG:3857",
            BackColor = new Color(13, 18, 27),
        };

        _baseLayer = CreateBaseLayer(EarthMapStyle.Standard);
        Map.Layers.Add(_baseLayer);

        _resultLayer = new MemoryLayer
        {
            Name = "Результат поиска",
            Features = [],
            Style = new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 1.1,
                Fill = new Brush(new Color(255, 77, 109)),
                Outline = new Pen(Color.White, 2),
            },
        };
        Map.Layers.Add(_resultLayer);

        _guessLayer = CreateMarkerLayer(
            "Выбранная точка",
            new Color(255, 196, 61),
            SymbolType.Triangle);
        _targetLayer = CreateMarkerLayer(
            "Правильная точка",
            new Color(54, 211, 153),
            SymbolType.Ellipse);
        Map.Layers.Add(_guessLayer);
        Map.Layers.Add(_targetLayer);
        Map.Widgets.Add(new ScaleBarWidget(Map)
        {
            Margin = new MRect(18),
            TextColor = Color.White,
            Halo = new Color(13, 18, 27),
        });
    }

    public Mapsui.Map Map { get; }

    public void SetMapStyle(EarthMapStyle style)
    {
        var newLayer = CreateBaseLayer(style);
        Map.Layers.Remove(_baseLayer);
        _baseLayer.Dispose();
        _baseLayer = newLayer;
        Map.Layers.Insert(0, _baseLayer);
        Map.Refresh();
    }

    public void NavigateTo(GeoCoordinate coordinate, string label)
    {
        var projected = SphericalMercator.FromLonLat(coordinate.Longitude, coordinate.Latitude).ToMPoint();
        var feature = new PointFeature(projected);
        feature["name"] = label;
        _resultLayer.Features = [feature];
        Map.Refresh();

        var resolutions = Map.Navigator.Resolutions;
        var resolution = resolutions.Count > 16 ? resolutions[16] : 9.554628535647032;
        Map.Navigator.CenterOnAndZoomTo(projected, resolution, 550);
    }

    public void SetGeoGuess(GeoCoordinate coordinate)
    {
        _guessLayer.Features = [CreatePointFeature(coordinate, "Ваш ответ")];
        _targetLayer.Features = [];
        Map.Refresh();
    }

    public void RevealGeoGuess(GeoCoordinate coordinate)
    {
        _targetLayer.Features = [CreatePointFeature(coordinate, "Правильное место")];
        Map.Refresh();
    }

    public void ClearGeoGuess()
    {
        _guessLayer.Features = [];
        _targetLayer.Features = [];
        Map.Refresh();
    }

    public void ShowWorld() => Map.Navigator.ZoomToPanBounds(duration: 450);

    public void ZoomIn() => Map.Navigator.ZoomIn(180);

    public void ZoomOut() => Map.Navigator.ZoomOut(180);

    public void SetDarkMode(bool enabled) =>
        Map.BackColor = enabled ? new Color(13, 18, 27) : new Color(224, 232, 241);

    public void Dispose() => Map.Dispose();

    private static MemoryLayer CreateMarkerLayer(string name, Color color, SymbolType symbolType) =>
        new()
        {
            Name = name,
            Features = [],
            Style = new SymbolStyle
            {
                SymbolType = symbolType,
                SymbolScale = 1.15,
                Fill = new Brush(color),
                Outline = new Pen(Color.White, 2),
            },
        };

    private static PointFeature CreatePointFeature(GeoCoordinate coordinate, string label)
    {
        var projected = SphericalMercator
            .FromLonLat(coordinate.Longitude, coordinate.Latitude)
            .ToMPoint();
        var feature = new PointFeature(projected);
        feature["name"] = label;
        return feature;
    }

    private static TileLayer CreateBaseLayer(EarthMapStyle style)
    {
        if (style == EarthMapStyle.Standard)
        {
            var layer = OpenStreetMap.CreateTileLayer(ApplicationUserAgent);
            layer.Name = "OpenStreetMap";
            return layer;
        }

        var attribution = new BruTile.Attribution(
            "© OpenTopoMap-R · © OpenStreetMap contributors",
            "https://openmaps.fr/tile-usage-policy.html");
        var source = new HttpTileSource(
            new GlobalSphericalMercator(),
            "https://tile.openmaps.fr/opentopomap/{z}/{x}/{y}.png",
            name: "OpenTopoMap-R",
            attribution: attribution,
            configureHttpRequestMessage: request =>
                request.Headers.TryAddWithoutValidation("User-Agent", ApplicationUserAgent));
        return new TileLayer(source) { Name = "OpenTopoMap-R" };
    }
}
