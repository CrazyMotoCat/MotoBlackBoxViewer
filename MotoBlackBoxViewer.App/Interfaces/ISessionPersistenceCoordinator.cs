using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Interfaces;

public interface ISessionPersistenceCoordinator
{
    AppSessionSettings Load();

    void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition);
}
