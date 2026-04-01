using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Interfaces;

public interface ISessionPersistenceCoordinator
{
    event Action<Exception>? SaveFailed;

    AppSessionSettings Load();

    void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition);

    void Flush(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition);
}
