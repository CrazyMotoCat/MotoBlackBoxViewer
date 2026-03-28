using System.Windows;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.App;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ICsvTelemetryReader reader = new CsvTelemetryReader();
        ITelemetryAnalyzer analyzer = new TelemetryAnalyzer();
        IMapExportService mapExportService = new MapExportService();
        IFileDialogService fileDialogService = new FileDialogService();
        IPlaybackService playbackService = new PlaybackService();
        IPlaybackCoordinator playbackCoordinator = new PlaybackCoordinator(playbackService);
        IAppSettingsService settingsService = new JsonAppSettingsService();
        ISessionPersistenceCoordinator sessionPersistenceCoordinator = new SessionPersistenceCoordinator(settingsService);

        var workspace = new TelemetryWorkspace(
            reader,
            analyzer,
            mapExportService,
            playbackCoordinator,
            sessionPersistenceCoordinator);

        _mainViewModel = new MainViewModel(workspace, fileDialogService);

        var mainWindow = new MainWindow(_mainViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();

        await _mainViewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.SaveSession();
        _mainViewModel?.Dispose();
        base.OnExit(e);
    }
}
