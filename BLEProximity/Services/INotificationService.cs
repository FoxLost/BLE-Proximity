namespace BLEProximity.Services;

/// <summary>
/// Abstraction for displaying modal notifications to the user.
/// Allows testability of components that need to show message boxes.
/// </summary>
public interface INotificationService
{
    void ShowError(string message, string title);
    void ShowWarning(string message, string title);
    string? ShowInputDialog(string title, string prompt, string defaultValue = "");
}
