using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using EarthExplorer.App.Services;
using EarthExplorer.Core.Models;
using EarthExplorer.Core.Services;
using EarthExplorer.Core.Utilities;

namespace EarthExplorer.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IGeocodingService _geocoding;
    private readonly IImageMetadataService _imageMetadata;
    private readonly IMapNavigationService _map;
    private readonly IFilePickerService _filePicker;
    private readonly IThemeService _theme;
    private readonly DispatcherTimer _resourceTimer;
    private string _searchText = string.Empty;
    private string _statusMessage = "Готово к поиску";
    private string _pointerCoordinates = "Курсор: —";
    private string _centerCoordinates = "Центр: 0.000000, 0.000000";
    private string _zoomText = "Масштаб: глобальный";
    private string _resourceText = "Модель выключена · VRAM свободна";
    private string? _photoPath;
    private string _photoMetadataSummary = "Загрузите оригинальную фотографию — у скриншотов обычно нет GPS.";
    private PlaceSearchResult? _selectedResult;
    private bool _isBusy;

    public MainViewModel(
        IGeocodingService geocoding,
        IImageMetadataService imageMetadata,
        IMapNavigationService map,
        IFilePickerService filePicker,
        IThemeService theme)
    {
        _geocoding = geocoding;
        _imageMetadata = imageMetadata;
        _map = map;
        _filePicker = filePicker;
        _theme = theme;

        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !string.IsNullOrWhiteSpace(SearchText));
        OpenSelectedResultCommand = new RelayCommand(OpenSelectedResult, () => SelectedResult is not null);
        ChoosePhotoCommand = new AsyncRelayCommand(ChoosePhotoAsync);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ShowWorldCommand = new RelayCommand(_map.ShowWorld);
        ZoomInCommand = new RelayCommand(_map.ZoomIn);
        ZoomOutCommand = new RelayCommand(_map.ZoomOut);

        _resourceTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, UpdateResourceText, Dispatcher.CurrentDispatcher);
        _resourceTimer.Start();
        UpdateResourceText(this, EventArgs.Empty);
    }

    public ObservableCollection<PlaceSearchResult> SearchResults { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string PointerCoordinates
    {
        get => _pointerCoordinates;
        private set => SetProperty(ref _pointerCoordinates, value);
    }

    public string CenterCoordinates
    {
        get => _centerCoordinates;
        private set => SetProperty(ref _centerCoordinates, value);
    }

    public string ZoomText
    {
        get => _zoomText;
        private set => SetProperty(ref _zoomText, value);
    }

    public string ResourceText
    {
        get => _resourceText;
        private set => SetProperty(ref _resourceText, value);
    }

    public string? PhotoPath
    {
        get => _photoPath;
        private set
        {
            if (SetProperty(ref _photoPath, value))
            {
                OnPropertyChanged(nameof(HasPhoto));
                OnPropertyChanged(nameof(PhotoName));
            }
        }
    }

    public string PhotoName => PhotoPath is null ? "Фото не выбрано" : Path.GetFileName(PhotoPath);

    public bool HasPhoto => PhotoPath is not null;

    public string PhotoMetadataSummary
    {
        get => _photoMetadataSummary;
        private set => SetProperty(ref _photoMetadataSummary, value);
    }

    public PlaceSearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                OpenSelectedResultCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedResultTitle));
                OnPropertyChanged(nameof(SelectedResultCoordinates));
            }
        }
    }

    public string SelectedResultTitle => SelectedResult?.DisplayName ?? "Выберите результат";

    public string SelectedResultCoordinates => SelectedResult?.CoordinateText ?? "Координаты появятся здесь";

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public AsyncRelayCommand SearchCommand { get; }

    public RelayCommand OpenSelectedResultCommand { get; }

    public AsyncRelayCommand ChoosePhotoCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public RelayCommand ShowWorldCommand { get; }

    public RelayCommand ZoomInCommand { get; }

    public RelayCommand ZoomOutCommand { get; }

    public async Task AnalyzePhotoAsync(string filePath)
    {
        IsBusy = true;
        StatusMessage = "Читаю метаданные локально…";
        try
        {
            var metadata = await _imageMetadata.ReadAsync(filePath, CancellationToken.None);
            PhotoPath = filePath;

            var details = new List<string>();
            if (metadata.Camera is not null)
            {
                details.Add($"Камера: {metadata.Camera}");
            }

            if (metadata.CapturedAt is not null)
            {
                details.Add($"Снято: {metadata.CapturedAt:dd.MM.yyyy HH:mm}");
            }

            if (metadata.AltitudeMeters is not null)
            {
                details.Add($"Высота: {metadata.AltitudeMeters:F0} м");
            }

            if (metadata.GpsCoordinate is { } coordinate)
            {
                details.Insert(0, $"GPS найден в EXIF: {coordinate}");
                var result = new PlaceSearchResult("Координаты из EXIF", "EXIF", coordinate);
                SearchResults.Clear();
                SearchResults.Add(result);
                SelectedResult = result;
                _map.NavigateTo(coordinate, result.DisplayName);
                StatusMessage = "Точка найдена в EXIF — фотография никуда не отправлялась";
            }
            else
            {
                details.Insert(0, "GPS в EXIF не найден. OCR и визуальный анализ появятся на следующем этапе.");
                StatusMessage = "Метаданные прочитаны локально";
            }

            PhotoMetadataSummary = string.Join(Environment.NewLine, details);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = "Не удалось прочитать фотографию";
            PhotoMetadataSummary = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void UpdatePointer(double longitude, double latitude) =>
        PointerCoordinates = $"Курсор: {latitude:F6}, {longitude:F6}";

    public void UpdateViewport(double longitude, double latitude, int zoomLevel) 
    {
        CenterCoordinates = $"Центр: {latitude:F6}, {longitude:F6}";
        ZoomText = $"Масштаб: z{zoomLevel}";
    }

    public void Dispose()
    {
        _resourceTimer.Stop();
        _resourceTimer.Tick -= UpdateResourceText;
    }

    private async Task SearchAsync()
    {
        IsBusy = true;
        StatusMessage = "Ищу место…";
        SearchResults.Clear();
        try
        {
            var coordinateResult = CoordinateParser.Parse(SearchText);
            if (coordinateResult.Success && coordinateResult.Coordinate is { } coordinate)
            {
                var result = new PlaceSearchResult("Введённые координаты", "координаты", coordinate);
                SearchResults.Add(result);
                SelectedResult = result;
                _map.NavigateTo(coordinate, result.DisplayName);
                StatusMessage = coordinateResult.WasSwapped
                    ? "Координаты распознаны; порядок широты и долготы исправлен"
                    : "Координаты распознаны";
                return;
            }

            var results = await _geocoding.SearchAsync(SearchText, CancellationToken.None);
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            if (SearchResults.Count == 0)
            {
                StatusMessage = "Ничего не найдено — уточните страну или регион";
                return;
            }

            SelectedResult = SearchResults[0];
            OpenSelectedResult();
            StatusMessage = $"Найдено вариантов: {SearchResults.Count}";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "Не удалось выполнить поиск: проверьте интернет и повторите";
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Сервис поиска не ответил вовремя";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenSelectedResult()
    {
        if (SelectedResult is not null)
        {
            _map.NavigateTo(SelectedResult.Coordinate, SelectedResult.DisplayName);
        }
    }

    private async Task ChoosePhotoAsync()
    {
        var filePath = _filePicker.PickImage();
        if (filePath is not null)
        {
            await AnalyzePhotoAsync(filePath);
        }
    }

    private void ToggleTheme()
    {
        _theme.Toggle();
        _map.SetDarkMode(_theme.IsDark);
    }

    private void UpdateResourceText(object? sender, EventArgs e)
    {
        using var process = Process.GetCurrentProcess();
        var ramMb = process.WorkingSet64 / 1024d / 1024d;
        ResourceText = $"Модель выключена · VRAM свободна · RAM {ramMb:F0} МБ";
    }
}
