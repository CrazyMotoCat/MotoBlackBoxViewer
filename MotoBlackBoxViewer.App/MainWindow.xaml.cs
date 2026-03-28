using System.Windows;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
