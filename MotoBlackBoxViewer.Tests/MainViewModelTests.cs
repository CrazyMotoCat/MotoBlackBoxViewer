using System.Windows.Input;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task OpenCsvCommand_WhenLoadFails_ShowsUserFacingErrorDialog()
    {
        StubUserNotificationService notificationService = new();
        using WorkspaceHarness harness = CreateHarness(
            reader: new ThrowingCsvTelemetryReader(new InvalidOperationException("broken csv")),
            notificationService: notificationService,
            pickedFilePath: "broken.csv");

        await ((AsyncRelayCommand)harness.ViewModel.OpenCsvCommand).ExecuteAsync();

        Assert.Equal("MotoBlackBoxViewer", notificationService.LastTitle);
        Assert.Contains("Не удалось открыть CSV-файл.", notificationService.LastMessage);
        Assert.Contains("broken csv", notificationService.LastMessage);
        Assert.Contains("broken csv", harness.ViewModel.Workspace.Data.StatusText);
    }

    [Fact]
    public async Task OpenCsvCommand_WhenLoadIsCanceled_DoesNotShowErrorDialog()
    {
        StubUserNotificationService notificationService = new();
        using WorkspaceHarness harness = CreateHarness(
            reader: new ThrowingCsvTelemetryReader(new OperationCanceledException("canceled")),
            notificationService: notificationService,
            pickedFilePath: "canceled.csv");

        await ((AsyncRelayCommand)harness.ViewModel.OpenCsvCommand).ExecuteAsync();

        Assert.Null(notificationService.LastTitle);
        Assert.Null(notificationService.LastMessage);
        Assert.Contains("отменена", harness.ViewModel.Workspace.Data.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspaceHarness CreateHarness(
        ICsvTelemetryReader reader,
        StubUserNotificationService notificationService,
        string? pickedFilePath)
    {
        TelemetryWorkspace workspace = new(
            reader,
            new TelemetryDataProcessor(new TelemetryAnalyzer()),
            new StubMapExportService(),
            new StubPlaybackCoordinator(),
            new RecordingSessionPersistenceCoordinator());
        MainViewModel viewModel = new(
            workspace,
            new StubFileDialogService(pickedFilePath),
            notificationService);

        return new WorkspaceHarness(viewModel);
    }

    private sealed class WorkspaceHarness : IDisposable
    {
        public WorkspaceHarness(MainViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public MainViewModel ViewModel { get; }

        public void Dispose() => ViewModel.Dispose();
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        private readonly string? _pickedFilePath;

        public StubFileDialogService(string? pickedFilePath)
        {
            _pickedFilePath = pickedFilePath;
        }

        public string? PickCsvFile() => _pickedFilePath;
    }

    private sealed class StubUserNotificationService : IUserNotificationService
    {
        public string? LastTitle { get; private set; }

        public string? LastMessage { get; private set; }

        public void ShowError(string title, string message)
        {
            LastTitle = title;
            LastMessage = message;
        }
    }

    private sealed class ThrowingCsvTelemetryReader : ICsvTelemetryReader
    {
        private readonly Exception _exception;

        public ThrowingCsvTelemetryReader(Exception exception)
        {
            _exception = exception;
        }

        public Task<CsvTelemetryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromException<CsvTelemetryReadResult>(_exception);
    }

    private sealed class StubMapExportService : IMapExportService
    {
        public string GetTemplatePath() => "template.html";

        public string BuildRouteJson(IReadOnlyList<TelemetryPoint> points)
            => $"[{string.Join(",", points.Select(p => p.Index))}]";

        public string ExportHtml(IReadOnlyList<TelemetryPoint> points, string outputDirectory)
            => Path.Combine(outputDirectory, "map.html");

        public void OpenInBrowser(string htmlPath)
        {
        }
    }

    private sealed class StubPlaybackCoordinator : IPlaybackCoordinator
    {
        public StubPlaybackCoordinator()
        {
            SpeedOptions =
            [
                new PlaybackSpeedOption("0.5x", 0.5),
                new PlaybackSpeedOption("1x", 1.0),
                new PlaybackSpeedOption("2x", 2.0)
            ];
            SelectedSpeed = SpeedOptions[1];
        }

        public event EventHandler? Tick
        {
            add { }
            remove { }
        }

        public IReadOnlyList<PlaybackSpeedOption> SpeedOptions { get; }

        public PlaybackSpeedOption SelectedSpeed { get; private set; }

        public int IntervalMilliseconds => (int)Math.Round(350d / SelectedSpeed.Multiplier);

        public bool IsRunning { get; private set; }

        public bool SetSelectedSpeed(PlaybackSpeedOption option)
        {
            if (Equals(option, SelectedSpeed))
                return false;

            SelectedSpeed = option;
            return true;
        }

        public void RestoreSpeed(string? label)
        {
            PlaybackSpeedOption? match = SpeedOptions.FirstOrDefault(option => option.Label == label);
            if (match is not null)
                SelectedSpeed = match;
        }

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingSessionPersistenceCoordinator : ISessionPersistenceCoordinator
    {
        public event Action<Exception>? SaveFailed
        {
            add { }
            remove { }
        }

        public AppSessionSettings Load() => new();

        public void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
        {
        }

        public void Flush(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
        {
        }
    }
}
