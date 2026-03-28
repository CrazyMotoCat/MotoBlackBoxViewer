using Microsoft.Win32;

namespace MotoBlackBoxViewer.App.Services;

public sealed class FileDialogService
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
