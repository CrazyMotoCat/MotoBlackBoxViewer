using System.Diagnostics;
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

        try
        {
            ICsvTelemetryReader reader = new CsvTelemetryReader();
            ITelemetryAnalyzer analyzer = new TelemetryAnalyzer();
            TelemetryDataProcessor dataProcessor = new(analyzer);
            IMapExportService mapExportService = new MapExportService();
            IFileDialogService fileDialogService = new FileDialogService();
            IUserNotificationService notificationService = new MessageBoxNotificationService();
            IPlaybackService playbackService = new PlaybackService();
            IPlaybackCoordinator playbackCoordinator = new PlaybackCoordinator(playbackService);
            IAppSettingsService settingsService = new JsonAppSettingsService();
            ISessionPersistenceCoordinator sessionPersistenceCoordinator = new SessionPersistenceCoordinator(settingsService);

            TelemetryWorkspace workspace = new(
                reader,
                dataProcessor,
                mapExportService,
                playbackCoordinator,
                sessionPersistenceCoordinator);

            _mainViewModel = new MainViewModel(workspace, fileDialogService, notificationService);

            MainWindow mainWindow = new(_mainViewModel);
            MainWindow = mainWindow;
            mainWindow.Show();

            await _mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Application startup failed: {ex}");
            MessageBox.Show(
                $"Application startup failed: {ex.Message}",
                "MotoBlackBoxViewer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.SaveSession();
        _mainViewModel?.Dispose();
        base.OnExit(e);
    }
}
