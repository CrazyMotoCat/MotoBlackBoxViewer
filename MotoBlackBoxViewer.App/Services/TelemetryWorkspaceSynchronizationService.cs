using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspaceSynchronizationService
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetrySelectionViewModel _selection;
    private readonly TelemetryMapViewModel _map;
    private readonly TelemetrySessionState _state;

    public TelemetryWorkspaceSynchronizationService(
        TelemetryDataViewModel data,
        TelemetrySelectionViewModel selection,
        TelemetryMapViewModel map,
        TelemetrySessionState state)
    {
        _data = data;
        _selection = selection;
        _map = map;
        _state = state;
    }

    public void SynchronizeAfterLoad(bool updateStatus)
    {
        TelemetryPoint? preferredPoint = _data.ApplyCurrentFilter(_state.SelectedPoint, updateStatus);
        _selection.SynchronizeWithVisiblePoints(preferredPoint);
        _map.RequestRefresh();
    }

    public void RestoreSessionView(AppSessionSettings session)
    {
        _data.RestoreFilterRange(session.FilterStartIndex, session.FilterEndIndex);

        TelemetryPoint? preferredPoint = _data.ApplyCurrentFilter(_state.SelectedPoint, updateStatus: false);
        _selection.SynchronizeWithVisiblePoints(preferredPoint);

        if (_data.HasPoints)
        {
            int restoredPosition = session.SelectedVisiblePosition <= 0
                ? 1
                : Math.Clamp(session.SelectedVisiblePosition, 1, _selection.PlaybackMaximum);

            _selection.PlaybackPosition = restoredPosition;
        }

        _map.RequestRefresh();
    }

    public void ClearWorkspace()
    {
        _data.Clear();
        _selection.Clear();
        _map.RequestRefresh();
    }
}
