using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace BLEProximity.Services;

/// <summary>
/// Displays and manages Windows toast notifications for the BLE Proximity countdown.
/// Uses Microsoft.Toolkit.Uwp.Notifications for toast content building and
/// ToastNotificationManager for targeted dismissal.
/// </summary>
public class ToastNotifier : IToastNotifier
{
    private const string ToastTag = "blelock_countdown";

    public bool IsToastVisible { get; private set; }

    /// <summary>
    /// Displays a countdown toast notification with the BLE Proximity warning.
    /// Shows title "BLE Proximity Warning", device name, command to execute,
    /// and a text indication of the countdown duration.
    /// Tags the toast with "blelock_countdown" for targeted dismissal.
    /// </summary>
    /// <param name="deviceName">Name of the device that went out of range.</param>
    /// <param name="command">The command that will be executed after countdown.</param>
    /// <param name="countdownSeconds">Duration of the countdown in seconds.</param>
    public void ShowCountdownToast(string deviceName, string command, int countdownSeconds)
    {
        try
        {
            new ToastContentBuilder()
                .AddText("BLE Proximity Warning")
                .AddText($"Device: {deviceName}")
                .AddText($"Command: {command}")
                .AddText($"Executing in {countdownSeconds} seconds...")
                .Show(toast =>
                {
                    toast.Tag = ToastTag;
                });

            IsToastVisible = true;
        }
        catch (Exception ex)
        {
            // Requirement 5.5: If toast fails to display due to system restrictions
            // or missing AppUserModelID shortcut, log the failure and proceed
            // without blocking command execution.
            Debug.WriteLine($"[ToastNotifier] Failed to show countdown toast: {ex.Message}");
            IsToastVisible = false;
        }
    }

    /// <summary>
    /// Dismisses the countdown toast notification using the "blelock_countdown" tag.
    /// Uses ToastNotificationManager.History.Remove for targeted dismissal.
    /// If dismissal fails, logs a warning and sets IsToastVisible to false.
    /// </summary>
    public void DismissCountdownToast()
    {
        try
        {
            ToastNotificationManagerCompat.History.Remove(ToastTag);
            IsToastVisible = false;
        }
        catch (Exception ex)
        {
            // Requirement 5.3: If toast dismissal fails, log warning.
            // The caller (ProximityMonitor) should abort countdown and return
            // to monitoring state without executing the command.
            Debug.WriteLine($"[ToastNotifier] Failed to dismiss countdown toast: {ex.Message}");
            IsToastVisible = false;
        }
    }
}
