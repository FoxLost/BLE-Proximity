using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BLEProximity.Models;
using Hardcodet.Wpf.TaskbarNotification;

namespace BLEProximity.Services;

public class TrayManager : ITrayManager
{
    private TaskbarIcon? _taskbarIcon;
    private DispatcherTimer? _refreshTimer;
    private WindowBehaviorManager? _windowBehaviorManager;

    private ProximityState _currentState = ProximityState.Cancelled;
    private string? _deviceName;
    private double? _rssi;
    private bool _isExecutionPaused;

    private double _lastWindowTop;
    private double _lastWindowLeft;
    private double _lastWindowWidth;
    private double _lastWindowHeight;
    private WindowState _lastWindowState = WindowState.Normal;

    public event EventHandler<bool>? ExecutionPauseChanged;
    public event EventHandler? ExitRequested;

    public void Initialize(TaskbarIcon taskbarIcon)
    {
        _taskbarIcon = taskbarIcon ?? throw new ArgumentNullException(nameof(taskbarIcon));

        // Hide Main_Window on start
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null)
        {
            SaveWindowPosition(mainWindow);
            mainWindow.ShowInTaskbar = false;
            mainWindow.Hide();
        }

        // Set initial tooltip
        UpdateTooltip();

        // Build initial context menu
        BuildContextMenu();

        // Set initial icon
        UpdateIcon();

        // Wire up left-click to restore window
        _taskbarIcon.TrayLeftMouseDown += OnTrayLeftMouseDown;

        // Tooltip can refresh quickly; context menu is rebuilt only on actual state changes.
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();
    }

    public void UpdateState(ProximityState state, string? deviceName, double? rssi)
    {
        _currentState = state;
        _deviceName = deviceName;
        _rssi = rssi;

        UpdateTooltip();
        BuildContextMenu();
        UpdateIcon();
    }

    public void SetExecutionPaused(bool isPaused)
    {
        if (_isExecutionPaused == isPaused)
            return;

        _isExecutionPaused = isPaused;
        UpdateTooltip();
        BuildContextMenu();
    }

    public void ShowBalloonTip(string title, string message)
    {
        _taskbarIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    /// <summary>
    /// Sets the WindowBehaviorManager to delegate window restore/exit operations.
    /// When set, tray left-click, Open Settings, and Exit will use the manager.
    /// </summary>
    public void SetWindowBehaviorManager(WindowBehaviorManager windowBehaviorManager)
    {
        _windowBehaviorManager = windowBehaviorManager;
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        if (_taskbarIcon == null) return;

        if (string.IsNullOrEmpty(_deviceName))
        {
            _taskbarIcon.ToolTipText = _isExecutionPaused
                ? "BLE Proximity - Paused, no device configured"
                : "BLE Proximity - No device configured";
        }
        else
        {
            var rssiText = _rssi.HasValue ? $"{_rssi.Value:F0}" : "N/A";
            var pauseText = _isExecutionPaused ? " | Execution paused" : string.Empty;
            _taskbarIcon.ToolTipText = $"BLE Proximity - Monitoring: {_deviceName} | RSSI: {rssiText} dBm{pauseText}";
        }
    }

    private void BuildContextMenu()
    {
        if (_taskbarIcon == null) return;

        var contextMenu = new ContextMenu();

        // App name header
        var headerItem = new MenuItem
        {
            Header = "BLE Proximity",
            FontWeight = FontWeights.Bold,
            IsEnabled = false
        };
        contextMenu.Items.Add(headerItem);

        // Device name with RSSI
        if (!string.IsNullOrEmpty(_deviceName))
        {
            var rssiText = _rssi.HasValue ? $"{_rssi.Value:F0}" : "N/A";
            var deviceItem = new MenuItem
            {
                Header = $"{_deviceName}: {rssiText} dBm",
                IsEnabled = false
            };
            contextMenu.Items.Add(deviceItem);
        }

        // Proximity state
        var stateText = GetProximityStateText();
        var stateItem = new MenuItem
        {
            Header = stateText,
            IsEnabled = false
        };
        contextMenu.Items.Add(stateItem);

        // Separator
        contextMenu.Items.Add(new Separator());

        var pauseItem = new MenuItem
        {
            Header = "Pause Execution",
            IsCheckable = true,
            IsChecked = _isExecutionPaused
        };
        pauseItem.Click += OnPauseExecutionClick;
        contextMenu.Items.Add(pauseItem);

        // "Open Settings"
        var openSettingsItem = new MenuItem { Header = "Open Settings" };
        openSettingsItem.Click += OnOpenSettingsClick;
        contextMenu.Items.Add(openSettingsItem);

        // Separator
        contextMenu.Items.Add(new Separator());

        // "Exit"
        var exitItem = new MenuItem { Header = "Exit Application" };
        exitItem.Click += OnExitClick;
        contextMenu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = contextMenu;
    }

    private string GetProximityStateText()
    {
        if (string.IsNullOrEmpty(_deviceName))
        {
            return "No Device";
        }

        return _currentState switch
        {
            ProximityState.InRange => "In Range",
            ProximityState.OutOfRangePending => "Out of Range Pending",
            ProximityState.Countdown => "Countdown",
            ProximityState.Executing => "Executing",
            ProximityState.OutOfRangeLatched => "Out of Range - Action Triggered",
            ProximityState.Cancelled => "No Device",
            _ => "No Device"
        };
    }

    private void UpdateIcon()
    {
        // Icon is set once at initialization from the exe's embedded icon.
        // State-based icon switching is disabled since pack:// resource icons
        // are placeholder files. The tooltip and context menu show the current state.
    }

    private Uri? GetIconUri()
    {
        if (string.IsNullOrEmpty(_deviceName))
        {
            return CreatePackUri("lock_gray.ico");
        }

        return _currentState switch
        {
            ProximityState.InRange => CreatePackUri("lock_green.ico"),
            ProximityState.OutOfRangePending => CreatePackUri("lock_yellow.ico"),
            ProximityState.Countdown => CreatePackUri("lock_red.ico"),
            ProximityState.Executing => CreatePackUri("lock_red.ico"),
            ProximityState.OutOfRangeLatched => CreatePackUri("lock_red.ico"),
            ProximityState.Cancelled => CreatePackUri("lock_gray.ico"),
            _ => CreatePackUri("lock_gray.ico")
        };
    }

    private static Uri CreatePackUri(string iconFileName)
    {
        return new Uri($"pack://application:,,,/Resources/{iconFileName}", UriKind.Absolute);
    }

    private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        if (_windowBehaviorManager != null)
        {
            _windowBehaviorManager.RestoreFromTray();
        }
        else
        {
            RestoreMainWindow();
        }
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_windowBehaviorManager != null)
        {
            _windowBehaviorManager.RestoreFromTray();
        }
        else
        {
            RestoreMainWindow();
        }
    }

    private void OnPauseExecutionClick(object sender, RoutedEventArgs e)
    {
        var requestedState = !_isExecutionPaused;
        SetExecutionPaused(requestedState);
        ExecutionPauseChanged?.Invoke(this, requestedState);
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);

        if (_windowBehaviorManager != null)
        {
            _refreshTimer?.Stop();
            _taskbarIcon?.Dispose();
            _windowBehaviorManager.ExitApplication();
        }
        else
        {
            _refreshTimer?.Stop();
            _taskbarIcon?.Dispose();
            Application.Current.Shutdown();
        }
    }

    private void RestoreMainWindow()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow == null) return;

        // Restore previous position and size
        mainWindow.Left = _lastWindowLeft;
        mainWindow.Top = _lastWindowTop;
        mainWindow.Width = _lastWindowWidth;
        mainWindow.Height = _lastWindowHeight;
        mainWindow.ShowInTaskbar = true;
        mainWindow.Show();
        mainWindow.WindowState = _lastWindowState == WindowState.Minimized
            ? WindowState.Normal
            : _lastWindowState;
        mainWindow.Activate();
    }

    private void SaveWindowPosition(Window window)
    {
        if (window.WindowState != WindowState.Minimized)
        {
            _lastWindowTop = window.Top;
            _lastWindowLeft = window.Left;
            _lastWindowWidth = window.Width;
            _lastWindowHeight = window.Height;
            _lastWindowState = window.WindowState;
        }
    }
}
