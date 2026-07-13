using System.IO;
using System.Windows;
using EarthExplorer.App.ViewModels;
using EarthExplorer.Map.Services;
using Mapsui;
using Mapsui.Projections;

namespace EarthExplorer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MapController _mapController;

    public MainWindow(MainViewModel viewModel, MapController mapController)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _mapController = mapController;
        DataContext = viewModel;

        EarthMap.Map = mapController.Map;
        EarthMap.MapPointerMoved += OnMapPointerMoved;
        mapController.Map.Navigator.ViewportChanged += OnViewportChanged;
        Closed += OnClosed;
    }

    private void OnMapPointerMoved(object? sender, MapEventArgs e)
    {
        var (longitude, latitude) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        if (double.IsFinite(latitude) && double.IsFinite(longitude))
        {
            _viewModel.UpdatePointer(longitude, latitude);
        }
    }

    private void OnViewportChanged(object sender, ViewportChangedEventArgs e)
    {
        var viewport = _mapController.Map.Navigator.Viewport;
        var (longitude, latitude) = SphericalMercator.ToLonLat(viewport.CenterX, viewport.CenterY);
        var resolutions = _mapController.Map.Navigator.Resolutions;
        var zoomLevel = 0;
        if (resolutions.Count > 0)
        {
            zoomLevel = Enumerable.Range(0, resolutions.Count)
                .MinBy(index => Math.Abs(resolutions[index] - viewport.Resolution));
        }

        if (double.IsFinite(latitude) && double.IsFinite(longitude))
        {
            _viewModel.UpdateViewport(longitude, latitude, zoomLevel);
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff",
        };
        var file = files.FirstOrDefault(path => allowed.Contains(Path.GetExtension(path)));
        if (file is not null)
        {
            await _viewModel.AnalyzePhotoAsync(file);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        EarthMap.MapPointerMoved -= OnMapPointerMoved;
        _mapController.Map.Navigator.ViewportChanged -= OnViewportChanged;
        Closed -= OnClosed;
    }
}
