using EarthExplorer.Core.Models;
using EarthExplorer.Core.Services;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Widgets.ScaleBar;

namespace EarthExplorer.Map.Services;

public sealed class MapController : IMapNavigationService, IDisposable
{
    private readonly MemoryLayer _resultLayer;

    public MapController()
    {
        Map = new Mapsui.Map
        {
            CRS = "EPSG:3857",
            BackColor = new Color(13, 18, 27),
        };

        var osmLayer = OpenStreetMap.CreateTileLayer();
        osmLayer.Name = "OpenStreetMap";
        Map.Layers.Add(osmLayer);

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
        Map.Widgets.Add(new ScaleBarWidget(Map)
        {
            Margin = new MRect(18),
            TextColor = Color.White,
            Halo = new Color(13, 18, 27),
        });
    }

    public Mapsui.Map Map { get; }

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

    public void ShowWorld() => Map.Navigator.ZoomToPanBounds(duration: 450);

    public void ZoomIn() => Map.Navigator.ZoomIn(180);

    public void ZoomOut() => Map.Navigator.ZoomOut(180);

    public void SetDarkMode(bool enabled) =>
        Map.BackColor = enabled ? new Color(13, 18, 27) : new Color(224, 232, 241);

    public void Dispose() => Map.Dispose();
}
