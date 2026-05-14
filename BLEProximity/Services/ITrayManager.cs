using BLEProximity.Models;
using Hardcodet.Wpf.TaskbarNotification;

namespace BLEProximity.Services;

public interface ITrayManager
{
    event EventHandler<bool>? ExecutionPauseChanged;
    event EventHandler? ExitRequested;
    void Initialize(TaskbarIcon taskbarIcon);
    void UpdateState(ProximityState state, string? deviceName, double? rssi);
    void SetExecutionPaused(bool isPaused);
    void ShowBalloonTip(string title, string message);
}
