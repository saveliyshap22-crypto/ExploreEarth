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
    private static readonly GeoGuessRound[] GeoGuessRounds =
    [
        new("Париж, Франция", "Железная башня 1889 года стоит рядом с рекой Сеной.", new GeoCoordinate(48.8584, 2.2945)),
        new("Нью-Йорк, США", "Медная статуя с факелом встречала суда у входа в гавань.", new GeoCoordinate(40.6892, -74.0445)),
        new("Сидней, Австралия", "Белые крыши этого здания напоминают паруса в гавани.", new GeoCoordinate(-33.8568, 151.2153)),
        new("Гиза, Египет", "Три древние пирамиды стоят у края огромной пустыни.", new GeoCoordinate(29.9792, 31.1342)),
        new("Токио, Япония", "Знаменитый диагональный перекрёсток окружён неоновыми экранами.", new GeoCoordinate(35.6595, 139.7005)),
        new("Рио-де-Жанейро, Бразилия", "Статуя с распростёртыми руками возвышается над океанским городом.", new GeoCoordinate(-22.9519, -43.2105)),
        new("Санкт-Петербург, Россия", "Зелёно-белый дворец выходит на огромную площадь у Невы.", new GeoCoordinate(59.9398, 30.3146)),
        new("Москва, Россия", "Красные стены, куранты и собор с разноцветными куполами.", new GeoCoordinate(55.7539, 37.6208)),
        new("Дубай, ОАЭ", "Самый высокий небоскрёб мира окружён фонтанами и пустынным мегаполисом.", new GeoCoordinate(25.1972, 55.2744)),
        new("Рим, Италия", "Огромный овальный амфитеатр построен почти две тысячи лет назад.", new GeoCoordinate(41.8902, 12.4922)),
    ];

    private readonly IGeocodingService _geocoding;
    private readonly IPhotoGeolocationService _photoGeolocation;
    private readonly IMapNavigationService _map;
    private readonly IFilePickerService _filePicker;
    private readonly IThemeService _theme;
    private readonly DispatcherTimer _resourceTimer;
    private CancellationTokenSource? _photoAnalysisCancellation;
    private AppMode _currentMode = AppMode.Map;
    private string _searchText = string.Empty;
    private string _statusMessage = "Готово к поиску";
    private string _pointerCoordinates = "Курсор: —";
    private string _centerCoordinates = "Центр: 0.000000, 0.000000";
    private string _zoomText = "Масштаб: глобальный";
    private string _resourceText = "OCR выгружен · VRAM свободна";
    private string? _photoPath;
    private string _photoMetadataSummary = "Выберите оригинальное фото или перетащите его в окно.";
    private string _recognizedText = "OCR-текст появится после анализа.";
    private string _mapAttributionTitle = "OpenStreetMap";
    private string _mapAttributionLine = "© участники OpenStreetMap";
    private string _geoGuessClue = "Начните раунд и поставьте метку на карте.";
    private string _geoGuessResult = "За один раунд можно получить до 5000 очков.";
    private string _geoGuessSelectionText = "Ответ ещё не выбран";
    private PlaceSearchResult? _selectedResult;
    private PhotoGeolocationHypothesis? _selectedPhotoHypothesis;
    private GeoGuessRound? _currentRound;
    private GeoCoordinate? _geoGuessSelection;
    private bool _isBusy;
    private bool _isPhotoAnalyzing;
    private bool _isGeoGuessRevealed;

    public MainViewModel(
        IGeocodingService geocoding,
        IPhotoGeolocationService photoGeolocation,
        IMapNavigationService map,
        IFilePickerService filePicker,
        IThemeService theme)
    {
        _geocoding = geocoding;
        _photoGeolocation = photoGeolocation;
        _map = map;
        _filePicker = filePicker;
        _theme = theme;

        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !string.IsNullOrWhiteSpace(SearchText));
        OpenSelectedResultCommand = new RelayCommand(OpenSelectedResult, () => SelectedResult is not null);
        ShowPhotoHypothesisCommand = new RelayCommand(ShowPhotoHypothesis, () => SelectedPhotoHypothesis is not null);
        ChoosePhotoCommand = new AsyncRelayCommand(ChoosePhotoAsync);
        AnalyzePhotoCommand = new AsyncRelayCommand(
            AnalyzeCurrentPhotoAsync,
            () => HasPhoto && !IsPhotoAnalyzing);
        CancelPhotoAnalysisCommand = new RelayCommand(CancelPhotoAnalysis, () => IsPhotoAnalyzing);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ShowWorldCommand = new RelayCommand(_map.ShowWorld);
        ZoomInCommand = new RelayCommand(_map.ZoomIn);
        ZoomOutCommand = new RelayCommand(_map.ZoomOut);

        ShowMapModeCommand = new RelayCommand(() => SetMode(AppMode.Map));
        ShowMapStylesModeCommand = new RelayCommand(() => SetMode(AppMode.MapStyles));
        ShowGeoGuesserModeCommand = new RelayCommand(() => SetMode(AppMode.GeoGuesser));
        ShowPhotoAiModeCommand = new RelayCommand(() => SetMode(AppMode.PhotoAi));
        UseStandardMapCommand = new RelayCommand(() => UseMapStyle(MapStyle.Standard));
        UseTopographicMapCommand = new RelayCommand(() => UseMapStyle(MapStyle.Topographic));
        StartGeoGuessRoundCommand = new RelayCommand(StartGeoGuessRound);
        SubmitGeoGuessCommand = new RelayCommand(
            SubmitGeoGuess,
            () => _currentRound is not null && _geoGuessSelection is not null && !IsGeoGuessRevealed);

        _resourceTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            UpdateResourceText,
            Dispatcher.CurrentDispatcher);
        _resourceTimer.Start();
        UpdateResourceText(this, EventArgs.Empty);
    }

    public ObservableCollection<PlaceSearchResult> SearchResults { get; } = [];

    public ObservableCollection<PhotoGeolocationHypothesis> PhotoHypotheses { get; } = [];

    public AppMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (!SetProperty(ref _currentMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsMapMode));
            OnPropertyChanged(nameof(IsMapStylesMode));
            OnPropertyChanged(nameof(IsGeoGuesserMode));
            OnPropertyChanged(nameof(IsPhotoAiMode));
        }
    }

    public bool IsMapMode => CurrentMode == AppMode.Map;

    public bool IsMapStylesMode => CurrentMode == AppMode.MapStyles;

    public bool IsGeoGuesserMode => CurrentMode == AppMode.GeoGuesser;

    public bool IsPhotoAiMode => CurrentMode == AppMode.PhotoAi;

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

    public string MapAttributionTitle
    {
        get => _mapAttributionTitle;
        private set => SetProperty(ref _mapAttributionTitle, value);
    }

    public string MapAttributionLine
    {
        get => _mapAttributionLine;
        private set => SetProperty(ref _mapAttributionLine, value);
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
                AnalyzePhotoCommand.RaiseCanExecuteChanged();
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

    public string RecognizedText
    {
        get => _recognizedText;
        private set => SetProperty(ref _recognizedText, value);
    }

    public string GeoGuessClue
    {
        get => _geoGuessClue;
        private set => SetProperty(ref _geoGuessClue, value);
    }

    public string GeoGuessResult
    {
        get => _geoGuessResult;
        private set => SetProperty(ref _geoGuessResult, value);
    }

    public string GeoGuessSelectionText
    {
        get => _geoGuessSelectionText;
        private set => SetProperty(ref _geoGuessSelectionText, value);
    }

    public bool IsGeoGuessRevealed
    {
        get => _isGeoGuessRevealed;
        private set
        {
            if (SetProperty(ref _isGeoGuessRevealed, value))
            {
                SubmitGeoGuessCommand.RaiseCanExecuteChanged();
            }
        }
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

    public PhotoGeolocationHypothesis? SelectedPhotoHypothesis
    {
        get => _selectedPhotoHypothesis;
        set
        {
            if (SetProperty(ref _selectedPhotoHypothesis, value))
            {
                ShowPhotoHypothesisCommand.RaiseCanExecuteChanged();
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

    public bool IsPhotoAnalyzing
    {
        get => _isPhotoAnalyzing;
        private set
        {
            if (SetProperty(ref _isPhotoAnalyzing, value))
            {
                AnalyzePhotoCommand.RaiseCanExecuteChanged();
                CancelPhotoAnalysisCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand SearchCommand { get; }

    public RelayCommand OpenSelectedResultCommand { get; }

    public RelayCommand ShowPhotoHypothesisCommand { get; }

    public AsyncRelayCommand ChoosePhotoCommand { get; }

    public AsyncRelayCommand AnalyzePhotoCommand { get; }

    public RelayCommand CancelPhotoAnalysisCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public RelayCommand ShowWorldCommand { get; }

    public RelayCommand ZoomInCommand { get; }

    public RelayCommand ZoomOutCommand { get; }

    public RelayCommand ShowMapModeCommand { get; }

    public RelayCommand ShowMapStylesModeCommand { get; }

    public RelayCommand ShowGeoGuesserModeCommand { get; }

    public RelayCommand ShowPhotoAiModeCommand { get; }

    public RelayCommand UseStandardMapCommand { get; }

    public RelayCommand UseTopographicMapCommand { get; }

    public RelayCommand StartGeoGuessRoundCommand { get; }

    public RelayCommand SubmitGeoGuessCommand { get; }

    public async Task AnalyzePhotoAsync(string filePath)
    {
        _photoAnalysisCancellation?.Cancel();
        var analysisCancellation = new CancellationTokenSource();
        _photoAnalysisCancellation = analysisCancellation;
        var cancellationToken = analysisCancellation.Token;

        SetMode(AppMode.PhotoAi);
        PhotoPath = filePath;
        PhotoHypotheses.Clear();
        SelectedPhotoHypothesis = null;
        RecognizedText = "Анализ выполняется…";
        IsBusy = true;
        IsPhotoAnalyzing = true;
        var progress = new Progress<string>(message => StatusMessage = message);

        try
        {
            var result = await _photoGeolocation.AnalyzeAsync(filePath, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            PhotoMetadataSummary = BuildMetadataSummary(result);
            RecognizedText = string.IsNullOrWhiteSpace(result.RecognizedText)
                ? "OCR-текст не найден или не требовался."
                : result.RecognizedText;

            foreach (var hypothesis in result.Hypotheses)
            {
                PhotoHypotheses.Add(hypothesis);
            }

            StatusMessage = result.Summary;
            if (PhotoHypotheses.Count > 0)
            {
                SelectedPhotoHypothesis = PhotoHypotheses[0];
                ShowPhotoHypothesis();
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Анализ фотографии отменён";
            RecognizedText = "Анализ отменён.";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "OCR завершён, но поиск места недоступен — проверьте интернет";
        }
        catch (Exception exception)
        {
            StatusMessage = "Не удалось проанализировать фотографию";
            PhotoMetadataSummary = exception.Message;
            RecognizedText = "OCR не выполнен.";
        }
        finally
        {
            if (ReferenceEquals(_photoAnalysisCancellation, analysisCancellation))
            {
                _photoAnalysisCancellation = null;
                IsPhotoAnalyzing = false;
                IsBusy = false;
            }

            analysisCancellation.Dispose();
        }
    }

    public void HandleMapTap(double longitude, double latitude)
    {
        if (!IsGeoGuesserMode || _currentRound is null || IsGeoGuessRevealed)
        {
            return;
        }

        var clampedLatitude = Math.Clamp(latitude, -85, 85);
        _geoGuessSelection = new GeoCoordinate(clampedLatitude, longitude);
        _map.SetGeoGuess(_geoGuessSelection.Value);
        GeoGuessSelectionText = $"Ваш ответ: {_geoGuessSelection.Value}";
        SubmitGeoGuessCommand.RaiseCanExecuteChanged();
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
        _photoAnalysisCancellation?.Cancel();
        _photoAnalysisCancellation?.Dispose();
    }

    private async Task SearchAsync()
    {
        SetMode(AppMode.Map);
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

    private void ShowPhotoHypothesis()
    {
        if (SelectedPhotoHypothesis is not null)
        {
            _map.NavigateTo(SelectedPhotoHypothesis.Coordinate, SelectedPhotoHypothesis.DisplayName);
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

    private Task AnalyzeCurrentPhotoAsync()
    {
        var photoPath = PhotoPath;
        return photoPath is null ? Task.CompletedTask : AnalyzePhotoAsync(photoPath);
    }

    private void CancelPhotoAnalysis() => _photoAnalysisCancellation?.Cancel();

    private void ToggleTheme()
    {
        _theme.Toggle();
        _map.SetDarkMode(_theme.IsDark);
    }

    private void SetMode(AppMode mode)
    {
        CurrentMode = mode;
        if (mode == AppMode.GeoGuesser && _currentRound is null)
        {
            StartGeoGuessRound();
        }
    }

    private void UseMapStyle(MapStyle style)
    {
        _map.SetMapStyle(style);
        if (style == MapStyle.Standard)
        {
            MapAttributionTitle = "OpenStreetMap";
            MapAttributionLine = "© участники OpenStreetMap";
            StatusMessage = "Включена стандартная карта";
        }
        else
        {
            MapAttributionTitle = "OpenTopoMap-R";
            MapAttributionLine = "© OpenTopoMap-R · © OpenStreetMap";
            StatusMessage = "Включена топографическая карта";
        }
    }

    private void StartGeoGuessRound()
    {
        GeoGuessRound next;
        do
        {
            next = GeoGuessRounds[Random.Shared.Next(GeoGuessRounds.Length)];
        }
        while (GeoGuessRounds.Length > 1 && ReferenceEquals(next, _currentRound));

        _currentRound = next;
        _geoGuessSelection = null;
        IsGeoGuessRevealed = false;
        GeoGuessClue = next.Clue;
        GeoGuessResult = "Поставьте метку на карте и подтвердите ответ.";
        GeoGuessSelectionText = "Ответ ещё не выбран";
        _map.ClearGeoGuess();
        _map.ShowWorld();
        SubmitGeoGuessCommand.RaiseCanExecuteChanged();
        StatusMessage = "Новый раунд GeoGuesser начат";
    }

    private void SubmitGeoGuess()
    {
        if (_currentRound is null || _geoGuessSelection is null)
        {
            return;
        }

        var distance = GeoDistance.BetweenKilometers(_geoGuessSelection.Value, _currentRound.Coordinate);
        var score = GeoDistance.GeoGuessScore(distance);
        IsGeoGuessRevealed = true;
        _map.RevealGeoGuess(_currentRound.Coordinate);
        GeoGuessResult = $"{_currentRound.PlaceName} · {distance:F0} км · {score} / 5000 очков";
        StatusMessage = "Ответ открыт: жёлтая метка — ваш выбор, зелёная — правильное место";
    }

    private static string BuildMetadataSummary(PhotoGeolocationResult result)
    {
        var details = new List<string> { result.Summary };
        if (result.Metadata.Camera is not null)
        {
            details.Add($"Камера: {result.Metadata.Camera}");
        }

        if (result.Metadata.CapturedAt is not null)
        {
            details.Add($"Снято: {result.Metadata.CapturedAt:dd.MM.yyyy HH:mm}");
        }

        if (result.Metadata.AltitudeMeters is not null)
        {
            details.Add($"Высота: {result.Metadata.AltitudeMeters:F0} м");
        }

        return string.Join(Environment.NewLine, details);
    }

    private void UpdateResourceText(object? sender, EventArgs e)
    {
        using var process = Process.GetCurrentProcess();
        var ramMb = process.WorkingSet64 / 1024d / 1024d;
        var ocrState = IsPhotoAnalyzing ? "OCR работает на CPU" : "OCR выгружен";
        ResourceText = $"{ocrState} · VRAM свободна · RAM {ramMb:F0} МБ";
    }
}
