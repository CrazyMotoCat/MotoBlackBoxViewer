using Microsoft.Win32;
using MotoBlackBoxViewer.App.Interfaces;

namespace MotoBlackBoxViewer.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? PickCsvFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Выберите CSV-файл телеметрии"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
