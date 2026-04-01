using System.Windows;
using MotoBlackBoxViewer.App.Interfaces;

namespace MotoBlackBoxViewer.App.Services;

public sealed class MessageBoxNotificationService : IUserNotificationService
{
    public void ShowError(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
