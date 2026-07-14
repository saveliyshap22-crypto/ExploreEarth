using System.Net.Http;
using System.Windows;
using EarthExplorer.AI.Services;
using EarthExplorer.App.Services;
using EarthExplorer.App.ViewModels;
using EarthExplorer.Core.Services;
using EarthExplorer.Infrastructure.Geocoding;
using EarthExplorer.Map.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EarthExplorer.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<MapController>();
        services.AddSingleton<IMapNavigationService>(provider => provider.GetRequiredService<MapController>());
        services.AddSingleton<IImageMetadataService, ImageMetadataService>();
        services.AddSingleton<IPhotoGeolocationService, LocalPhotoGeolocationService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.Timeout = TimeSpan.FromSeconds(12);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ExploreEarth/0.3 (+https://github.com/saveliyshap22-crypto/ExploreEarth)");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        });
        services.AddHttpClient<PanoramaxService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(12);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ExploreEarth/0.3 (+https://github.com/saveliyshap22-crypto/ExploreEarth)");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        });
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
