using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using EarthExplorer.App.ViewModels;
using EarthExplorer.Map.Services;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Microsoft.Web.WebView2.Core;

namespace EarthExplorer.App;

public partial class MainWindow : Window
{
    private const string PegmanDragFormat = "ExploreEarth/Pegman";
    private readonly MainViewModel _viewModel;
    private readonly MapController _mapController;
    private Point _pegmanMouseDown;
    private bool _globeInitialized;

    public MainWindow(MainViewModel viewModel, MapController mapController)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _mapController = mapController;
        DataContext = viewModel;

        EarthMap.Map = mapController.Map;
        EarthMap.MapPointerMoved += OnMapPointerMoved;
        EarthMap.MapTapped += OnMapTapped;
        mapController.Map.Navigator.ViewportChanged += OnViewportChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.StreetViewerRequested += OnStreetViewerRequested;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await EnsureGlobeInitializedAsync();
    }

    private async Task EnsureGlobeInitializedAsync()
    {
        if (_globeInitialized)
        {
            return;
        }

        try
        {
            await GlobeWebView.EnsureCoreWebView2Async();
            GlobeWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            GlobeWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            GlobeWebView.CoreWebView2.WebMessageReceived += OnGlobeMessageReceived;

            var globePath = Path.Combine(AppContext.BaseDirectory, "Web", "globe.html");
            if (!File.Exists(globePath))
            {
                _viewModel.UpdatePointer(0, 0);
                return;
            }

            GlobeWebView.Source = new Uri(globePath);
            _globeInitialized = true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "Для 3D Земли и панорам нужен Microsoft Edge WebView2 Runtime. В Windows 10/11 он обычно уже установлен.",
                "ExploreEarth",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsGlobeMode) && _viewModel.IsGlobeMode)
        {
            await EnsureGlobeInitializedAsync();
        }
    }

    private void OnGlobeMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement) || typeElement.GetString() != "tap")
            {
                return;
            }

            var latitude = root.GetProperty("latitude").GetDouble();
            var longitude = root.GetProperty("longitude").GetDouble();
            if (double.IsFinite(latitude) && double.IsFinite(longitude))
            {
                _viewModel.UpdatePointer(longitude, latitude);
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages from the embedded page.
        }
    }

    private void OnMapPointerMoved(object? sender, MapEventArgs e)
    {
        var (longitude, latitude) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        if (double.IsFinite(latitude) && double.IsFinite(longitude))
        {
            _viewModel.UpdatePointer(longitude, latitude);
        }
    }

    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        var (longitude, latitude) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        if (double.IsFinite(latitude) && double.IsFinite(longitude))
        {
            _viewModel.HandleMapTap(longitude, latitude);
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

    private void OnPegmanMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pegmanMouseDown = e.GetPosition(Pegman);
    }

    private void OnPegmanMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(Pegman);
        if (Math.Abs(current.X - _pegmanMouseDown.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _pegmanMouseDown.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(PegmanDragFormat, true);
        DragDrop.DoDragDrop(Pegman, data, DragDropEffects.Copy);
    }

    private void OnMapDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(PegmanDragFormat) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPegmanDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(PegmanDragFormat))
        {
            return;
        }

        var screen = e.GetPosition(EarthMap);
        var world = _mapController.Map.Navigator.Viewport.ScreenToWorld(screen.X, screen.Y);
        var (longitude, latitude) = SphericalMercator.ToLonLat(world.X, world.Y);
        if (double.IsFinite(latitude) && double.IsFinite(longitude))
        {
            await _viewModel.OpenStreetAtAsync(longitude, latitude);
        }

        e.Handled = true;
    }

    private void OnOpenStreetViewer(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenStreetViewer();
    }

    private async void OnStreetViewerRequested(Uri uri)
    {
        try
        {
            await StreetWebView.EnsureCoreWebView2Async();
            StreetWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            StreetWebView.Source = uri;
            StreetViewerPanel.Visibility = Visibility.Visible;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "Не найден Microsoft Edge WebView2 Runtime.",
                "ExploreEarth",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void OnCloseStreetViewer(object sender, RoutedEventArgs e)
    {
        StreetViewerPanel.Visibility = Visibility.Collapsed;
        StreetWebView.Source = new Uri("about:blank");
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
        EarthMap.MapTapped -= OnMapTapped;
        _mapController.Map.Navigator.ViewportChanged -= OnViewportChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.StreetViewerRequested -= OnStreetViewerRequested;
        if (GlobeWebView.CoreWebView2 is not null)
        {
            GlobeWebView.CoreWebView2.WebMessageReceived -= OnGlobeMessageReceived;
        }

        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }
}
