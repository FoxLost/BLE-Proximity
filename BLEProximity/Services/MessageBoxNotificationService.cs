using System.Windows;
using Microsoft.VisualBasic;

namespace BLEProximity.Services;

/// <summary>
/// Default implementation of INotificationService using WPF MessageBox.
/// </summary>
public class MessageBoxNotificationService : INotificationService
{
    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowWarning(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public string? ShowInputDialog(string title, string prompt, string defaultValue = "")
    {
        // Using Microsoft.VisualBasic.Interaction.InputBox for simplicity
        // In a production app, you might want to create a custom WPF dialog
        var result = Interaction.InputBox(prompt, title, defaultValue);
        return string.IsNullOrEmpty(result) ? null : result;
    }
}
