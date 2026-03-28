using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Interfaces;

public interface IAppSettingsService
{
    AppSessionSettings Load();
    void Save(AppSessionSettings settings);
}
