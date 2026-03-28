using System.ComponentModel;
using System.Windows.Input;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Interfaces;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class MainViewModel : IDisposable
{
    private readonly IFileDialogService _fileDialogService;
    private bool _isDisposed;

    public MainViewModel(TelemetryWorkspace workspace, IFileDialogService fileDialogService)
    {
        Workspace = workspace;
        _fileDialogService = fileDialogService;

        OpenCsvCommand = new AsyncRelayCommand(OpenCsvFromDialogAsync);
        RefreshMapCommand = new RelayCommand(Workspace.RequestMapRefresh, () => Workspace.HasPoints);
        OpenMapCommand = new RelayCommand(Workspace.OpenMapInBrowser, () => Workspace.HasPoints);
        ClearCommand = new RelayCommand(Workspace.Clear);
        ResetFilterCommand = new RelayCommand(Workspace.ResetFilter, () => Workspace.HasSourceData);
        PrevPointCommand = new RelayCommand(() => MoveSelection(-1), () => Workspace.HasPoints);
        NextPointCommand = new RelayCommand(() => MoveSelection(1), () => Workspace.HasPoints);
        TogglePlaybackCommand = new RelayCommand(Workspace.TogglePlayback, () => Workspace.HasPoints);

        Workspace.PropertyChanged += Workspace_PropertyChanged;
    }

    public TelemetryWorkspace Workspace { get; }

    public ICommand OpenCsvCommand { get; }

    public ICommand RefreshMapCommand { get; }

    public ICommand OpenMapCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand ResetFilterCommand { get; }

    public ICommand PrevPointCommand { get; }

    public ICommand NextPointCommand { get; }

    public ICommand TogglePlaybackCommand { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => Workspace.InitializeAsync(cancellationToken);

    public void SaveSession()
        => Workspace.SaveSession();

    private async Task OpenCsvFromDialogAsync()
    {
        string? filePath = _fileDialogService.PickCsvFile();
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            Workspace.StopPlayback(updateStatus: false);
            await Workspace.LoadCsvAsync(filePath);
        }
        catch (Exception ex)
        {
            Workspace.Data.StatusText = $"Ошибка загрузки CSV: {ex.Message}";
        }
    }

    private void MoveSelection(int delta)
    {
        Workspace.StopPlayback(updateStatus: false);
        Workspace.MoveSelection(delta);
    }

    private void Workspace_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TelemetryWorkspace.HasPoints) or nameof(TelemetryWorkspace.HasSourceData))
            RaiseCommandCanExecuteChanged();
    }

    private void RaiseCommandCanExecuteChanged()
    {
        (RefreshMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetFilterCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PrevPointCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextPointCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TogglePlaybackCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Workspace.PropertyChanged -= Workspace_PropertyChanged;
        Workspace.Dispose();
    }
}
