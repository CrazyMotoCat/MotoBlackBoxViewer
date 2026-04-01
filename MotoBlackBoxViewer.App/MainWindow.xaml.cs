using System.Windows;
using System.Windows.Input;
using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void PlaybackSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Workspace.Map.SetManualScrubbing(true);
    }

    private void PlaybackSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Workspace.Map.SetManualScrubbing(false);
    }

    private void PlaybackSlider_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Workspace.Map.SetManualScrubbing(false);
    }

    private void MapViewControl_ErrorOccurred(object sender, MapControlErrorEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Workspace.Data.StatusText = e.Message;
    }
}
