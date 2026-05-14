using System.IO;
using System.Security;
using Microsoft.Win32;

namespace BLEProximity.Services;

/// <summary>
/// Manages Windows auto-start registration via the registry.
/// Reads/writes the HKCU\Software\Microsoft\Windows\CurrentVersion\Run key.
/// </summary>
public class StartupManager : IStartupManager
{
    private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DefaultAppName = "BLEProximity";

    private readonly string _appName;
    private readonly string _executablePath;
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Creates a StartupManager with default app name and executable path.
    /// </summary>
    public StartupManager()
        : this(DefaultAppName, GetCurrentExecutablePath(), new MessageBoxNotificationService())
    {
    }

    /// <summary>
    /// Creates a StartupManager with configurable parameters for testability.
    /// </summary>
    public StartupManager(string appName, string executablePath, INotificationService notificationService)
    {
        _appName = appName;
        _executablePath = executablePath;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Checks whether the registry entry exists for auto-start.
    /// Returns true if the registry value exists under the Run key.
    /// </summary>
    public bool IsStartupEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, writable: false);
                if (key == null)
                    return false;

                return key.GetValue(_appName) != null;
            }
            catch (SecurityException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Enables or disables auto-start by creating or removing the registry entry.
    /// Returns true on success, false on failure.
    /// On failure, attempts to revert the toggle; if reversion also fails, shows a modal error dialog.
    /// </summary>
    public bool SetStartupEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                EnableStartup();
            }
            else
            {
                DisableStartup();
            }

            return true;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Attempt to revert toggle to previous state
            if (!TryRevert(!enabled))
            {
                // Reversion also failed - display modal error dialog
                _notificationService.ShowError(
                    $"Failed to update the Windows startup registry entry.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Elevated permissions may be required to modify startup settings.",
                    "Startup Registry Error");
            }

            return false;
        }
    }

    private void EnableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, writable: true)
            ?? throw new IOException($"Unable to open registry key: {RegistryRunPath}");

        key.SetValue(_appName, _executablePath);
    }

    private void DisableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, writable: true)
            ?? throw new IOException($"Unable to open registry key: {RegistryRunPath}");

        key.DeleteValue(_appName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Attempts to revert the registry to the previous state.
    /// Returns true if reversion succeeded, false if it also failed.
    /// </summary>
    private bool TryRevert(bool revertToEnabled)
    {
        try
        {
            if (revertToEnabled)
            {
                EnableStartup();
            }
            else
            {
                DisableStartup();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetCurrentExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
            return processPath;

        // Assembly.Location is empty in single-file publishes; the process module keeps the real executable path.
        return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? AppContext.BaseDirectory;
    }
}
