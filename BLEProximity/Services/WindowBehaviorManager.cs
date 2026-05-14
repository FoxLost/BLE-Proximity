using System.Windows;

namespace BLEProximity.Services;

/// <summary>
/// Manages window minimize/close behavior for the MainWindow.
/// Ensures the window hides to system tray on minimize and close,
/// shows a one-time balloon tip on first close, and handles proper
/// application exit with cleanup.
/// </summary>
public class WindowBehaviorManager
{
    private readonly ITrayManager _trayManager;
    private readonly IBleScanner _bleScanner;
    private Window? _window;
    private bool _hasShownFirstCloseBalloon;
    private bool _isExiting;

    // Store window position/size for restore
    private double _lastWindowTop;
    private double _lastWindowLeft;
    private double _lastWindowWidth;
    private double _lastWindowHeight;
    private WindowState _lastWindowState = WindowState.Normal;

    public WindowBehaviorManager(ITrayManager trayManager, IBleScanner bleScanner)
    {
        _trayManager = trayManager ?? throw new ArgumentNullException(nameof(trayManager));
        _bleScanner = bleScanner ?? throw new ArgumentNullException(nameof(bleScanner));
    }

    /// <summary>
    /// Attaches the window behavior manager to a Window instance.
    /// Call this after the window is initialized.
    /// </summary>
    public void Attach(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        // Save initial position
        SaveWindowPosition();

        // Subscribe to window events
        _window.StateChanged += OnWindowStateChanged;
        _window.Closing += OnWindowClosing;
        _window.LocationChanged += OnWindowLocationChanged;
        _window.SizeChanged += OnWindowSizeChanged;
    }

    /// <summary>
    /// Detaches the window behavior manager from the current window.
    /// </summary>
    public void Detach()
    {
        if (_window == null) return;

        _window.StateChanged -= OnWindowStateChanged;
        _window.Closing -= OnWindowClosing;
        _window.LocationChanged -= OnWindowLocationChanged;
        _window.SizeChanged -= OnWindowSizeChanged;
        _window = null;
    }

    /// <summary>
    /// Hides the window to the system tray.
    /// Sets ShowInTaskbar=false to prevent Alt+Tab visibility.
    /// </summary>
    public void HideToTray()
    {
        if (_window == null) return;

        SaveWindowPosition();
        _window.ShowInTaskbar = false;
        _window.Hide();
    }

    /// <summary>
    /// Restores the window from the system tray to its previous size and position.
    /// Sets ShowInTaskbar=true and brings the window to the foreground.
    /// </summary>
    public void RestoreFromTray()
    {
        if (_window == null) return;

        _window.Left = _lastWindowLeft;
        _window.Top = _lastWindowTop;
        _window.Width = _lastWindowWidth;
        _window.Height = _lastWindowHeight;
        _window.ShowInTaskbar = true;
        _window.Show();
        _window.WindowState = _lastWindowState == WindowState.Minimized
            ? WindowState.Normal
            : _lastWindowState;
        _window.Activate();
    }

    /// <summary>
    /// Performs a full application exit: stops BLE scanner, disposes resources,
    /// removes tray icon, and terminates the process.
    /// </summary>
    public void ExitApplication()
    {
        _isExiting = true;

        // Stop BLE scanner and dispose (disposes BluetoothLEAdvertisementWatcher)
        _bleScanner.Stop();
        _bleScanner.Dispose();

        // Shutdown the application (TrayManager cleanup is handled via App shutdown)
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Gets whether the first-close balloon tip has already been shown.
    /// </summary>
    public bool HasShownFirstCloseBalloon => _hasShownFirstCloseBalloon;

    /// <summary>
    /// Allows resetting the first-close balloon flag (for testing purposes).
    /// </summary>
    internal void ResetFirstCloseBalloon()
    {
        _hasShownFirstCloseBalloon = false;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_window == null) return;

        // When minimized, hide to tray instead
        if (_window.WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_window == null) return;

        // If we're actually exiting, allow the close
        if (_isExiting || IsExplicitExitRequested())
        {
            return;
        }

        // Cancel the close and hide to tray instead
        e.Cancel = true;
        HideToTray();

        // Show first-close balloon tip if not shown before
        if (!_hasShownFirstCloseBalloon)
        {
            _hasShownFirstCloseBalloon = true;
            _trayManager.ShowBalloonTip(
                "BLE Proximity",
                "The application is still running in the system tray. Right-click the tray icon for options.");
        }
    }

    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        SaveWindowPosition();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SaveWindowPosition();
    }

    private void SaveWindowPosition()
    {
        if (_window == null) return;

        // Only save position when window is in a normal or maximized state
        if (_window.WindowState != WindowState.Minimized && _window.IsVisible)
        {
            _lastWindowTop = _window.Top;
            _lastWindowLeft = _window.Left;
            _lastWindowWidth = _window.Width;
            _lastWindowHeight = _window.Height;
            _lastWindowState = _window.WindowState;
        }
    }

    private static bool IsExplicitExitRequested()
    {
        return Application.Current?.Properties["ExplicitExitRequested"] is true;
    }
}
