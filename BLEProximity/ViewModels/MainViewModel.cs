using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BLEProximity.Helpers;
using BLEProximity.Models;
using BLEProximity.Services;

namespace BLEProximity.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IBleScanner _bleScanner;
    private readonly IRssiSmoother _rssiSmoother;
    private readonly IProximityMonitor _proximityMonitor;
    private readonly IToastNotifier _toastNotifier;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IConfigManager _configManager;
    private readonly IStartupManager _startupManager;
    private readonly ITrayManager _trayManager;
    private readonly INotificationService _notificationService;

    private readonly object _scannedDevicesLock = new();
    private readonly object _trustedSignalLock = new();
    private readonly DispatcherTimer _deviceScanTimer;
    private readonly DispatcherTimer _trustedSignalWatchdogTimer;
    private readonly Dictionary<string, DateTime> _trustedDeviceLastSeenUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ulong> _trustedDeviceBluetoothAddresses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _trustedDeviceMissingAlerts = new(StringComparer.OrdinalIgnoreCase);
    private int _deviceScanSecondsRemaining;
    private const int ManualDeviceScanDurationSec = 20;
    private const int DefaultMissingBeaconGraceSec = 3;

    public ObservableCollection<ScannedDevice> ScannedDevices => _bleScanner.ScannedDevices;

    [ObservableProperty]
    private ObservableCollection<TrustedDevice> _trustedDevices = new();

    [ObservableProperty]
    private ProximityState _currentProximityState;

    // Configuration fields
    [ObservableProperty]
    private int _inRangeThreshold = -70;

    [ObservableProperty]
    private int _outOfRangeThreshold = -75;

    [ObservableProperty]
    private int _outOfRangeTimeoutSec = 10;

    [ObservableProperty]
    private double _rssiAlpha = 0.3;

    [ObservableProperty]
    private int _gracePeriodSec = 5;

    [ObservableProperty]
    private int _missingBeaconGraceSec = DefaultMissingBeaconGraceSec;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _useMultiDevice;

    [ObservableProperty]
    private string _commandPreset = "LockWorkstation";

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string _statusText = "Idle - No trusted device configured";

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private ICollectionView? _scannedDevicesView;

    [ObservableProperty]
    private bool _isDeviceScanActive;

    [ObservableProperty]
    private bool _isExecutionPaused;

    [ObservableProperty]
    private string _deviceScanStatusText = "Monitoring trusted devices only.";

    public string DeviceScanButtonText => IsDeviceScanActive
        ? $"Scanning... {_deviceScanSecondsRemaining}s"
        : "Scan Devices";

    public string ExecutionPauseButtonText => IsExecutionPaused
        ? "Resume Execution"
        : "Pause Execution";

    public ObservableCollection<string> LogEntries { get; } = new();

    public IReadOnlyList<string> AvailablePresets { get; } = CommandPresets.Presets.Keys.ToList();

    public MainViewModel(
        IBleScanner bleScanner,
        IRssiSmoother rssiSmoother,
        IProximityMonitor proximityMonitor,
        IToastNotifier toastNotifier,
        ICommandExecutor commandExecutor,
        IConfigManager configManager,
        IStartupManager startupManager,
        ITrayManager trayManager,
        INotificationService notificationService)
    {
        _bleScanner = bleScanner;
        _rssiSmoother = rssiSmoother;
        _proximityMonitor = proximityMonitor;
        _toastNotifier = toastNotifier;
        _commandExecutor = commandExecutor;
        _configManager = configManager;
        _startupManager = startupManager;
        _trayManager = trayManager;
        _notificationService = notificationService;

        _deviceScanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _deviceScanTimer.Tick += OnDeviceScanTimerTick;

        _trustedSignalWatchdogTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _trustedSignalWatchdogTimer.Tick += OnTrustedSignalWatchdogTick;

        // Enable cross-thread access to the ScannedDevices collection
        BindingOperations.EnableCollectionSynchronization(ScannedDevices, _scannedDevicesLock);

        // Set up sorted view for ScannedDevices
        ScannedDevicesView = CollectionViewSource.GetDefaultView(ScannedDevices);
        ScannedDevicesView.SortDescriptions.Add(
            new SortDescription(nameof(ScannedDevice.SmoothedRssi), ListSortDirection.Descending));

        // Load configuration
        LoadConfiguration();

        // Wire events
        _bleScanner.AdvertisementReceived += OnAdvertisementReceived;
        _proximityMonitor.StateChanged += OnProximityStateChanged;
        _trayManager.ExecutionPauseChanged += OnTrayExecutionPauseChanged;
        _trayManager.ExitRequested += OnTrayExitRequested;
        _trustedSignalWatchdogTimer.Start();
    }

    private void LoadConfiguration()
    {
        var config = _configManager.Load();

        InRangeThreshold = config.InRangeThreshold;
        OutOfRangeThreshold = config.OutOfRangeThreshold;
        OutOfRangeTimeoutSec = config.OutOfRangeTimeoutSec;
        RssiAlpha = config.RssiAlpha;
        GracePeriodSec = config.GracePeriodSec;
        MissingBeaconGraceSec = Math.Clamp(config.MissingBeaconGraceSec, 1, 30);
        StartWithWindows = _startupManager.IsStartupEnabled;
        UseMultiDevice = config.UseMultiDevice;
        IsDarkMode = config.DarkMode;
        CommandPreset = CommandPresets.Presets.ContainsKey(config.CommandPreset)
            ? config.CommandPreset
            : "LockWorkstation";

        // Load command config from preset or custom
        if (CommandPresets.Presets.TryGetValue(CommandPreset, out var preset))
        {
            ExecutablePath = preset.ExecutablePath;
            Arguments = preset.Arguments;
        }
        else if (config.CustomCommand != null)
        {
            ExecutablePath = config.CustomCommand.ExecutablePath;
            Arguments = config.CustomCommand.Arguments;
        }

        // Load trusted devices
        TrustedDevices.Clear();
        foreach (var td in config.TrustedDevices)
        {
            TrustedDevices.Add(new TrustedDevice
            {
                Name = td.Name,
                MacAddress = td.MacAddress
            });
        }

        SyncTrustedDevicesWithScanner();

        // Configure proximity monitor
        _proximityMonitor.Configure(new ProximityConfig
        {
            InRangeThreshold = config.InRangeThreshold,
            OutOfRangeThreshold = config.OutOfRangeThreshold,
            OutOfRangeTimeoutSec = config.OutOfRangeTimeoutSec,
            GracePeriodSec = config.GracePeriodSec
        });

        // Configure smoother alpha
        _rssiSmoother.Alpha = config.RssiAlpha;
    }

    partial void OnCommandPresetChanged(string value)
    {
        if (CommandPresets.Presets.TryGetValue(value, out var preset))
        {
            ExecutablePath = preset.ExecutablePath;
            Arguments = preset.Arguments;
        }
    }

    private void OnAdvertisementReceived(object? sender, BleAdvertisementReceivedEventArgs e)
    {
        // Only forward RSSI to ProximityMonitor for trusted devices
        if (TrustedDevices.Any(td => td.MacAddress.Equals(e.MacAddress, StringComparison.OrdinalIgnoreCase)))
        {
            var wasMissing = false;
            lock (_trustedSignalLock)
            {
                _trustedDeviceLastSeenUtc[e.MacAddress] = e.Timestamp;
                _trustedDeviceBluetoothAddresses[e.MacAddress] = e.BluetoothAddress;
                wasMissing = _trustedDeviceMissingAlerts.Remove(e.MacAddress);
            }

            if (wasMissing)
            {
                AddLog($"Trusted device \"{e.Name}\" beacon restored.");
            }

            _proximityMonitor.UpdateRssi(e.BluetoothAddress, e.SmoothedRssi);

            // Update status text on UI thread
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UpdateStatusText(CurrentProximityState);
            });
        }

        // Refresh the sorted view
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ScannedDevicesView?.Refresh();
        });
    }

    private void OnProximityStateChanged(object? sender, ProximityStateChangedEventArgs e)
    {
        Console.WriteLine($"[MainViewModel] ProximityStateChanged: {e.OldState} -> {e.NewState}");

        try
        {
            if (e.NewState == ProximityState.Executing)
            {
                StartCommandExecutionTask();
            }

            RunOnUiThread(() => ProcessProximityStateChangedOnUiThread(e.OldState, e.NewState));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] EXCEPTION handling proximity state change: {ex}");
        }
    }

    private void OnDeviceScanTimerTick(object? sender, EventArgs e)
    {
        _deviceScanSecondsRemaining--;
        if (_deviceScanSecondsRemaining <= 0)
        {
            StopDeviceScan();
            return;
        }

        DeviceScanStatusText = $"Scanning nearby BLE devices for {_deviceScanSecondsRemaining}s...";
        OnPropertyChanged(nameof(DeviceScanButtonText));
    }

    private void OnTrustedSignalWatchdogTick(object? sender, EventArgs e)
    {
        if (TrustedDevices.Count == 0 || CurrentProximityState == ProximityState.Executing)
            return;

        var now = DateTime.UtcNow;
        foreach (var trustedDevice in TrustedDevices.ToList())
        {
            if (string.IsNullOrWhiteSpace(trustedDevice.MacAddress))
                continue;

            DateTime lastSeenUtc;
            ulong bluetoothAddress;
            bool shouldLogMissing;
            lock (_trustedSignalLock)
            {
                if (!_trustedDeviceLastSeenUtc.TryGetValue(trustedDevice.MacAddress, out lastSeenUtc))
                    continue;

                bluetoothAddress = _trustedDeviceBluetoothAddresses.TryGetValue(trustedDevice.MacAddress, out var address)
                    ? address
                    : trustedDevice.BluetoothAddress;

                shouldLogMissing = now - lastSeenUtc >= TimeSpan.FromSeconds(MissingBeaconGraceSec)
                    && _trustedDeviceMissingAlerts.Add(trustedDevice.MacAddress);
            }

            if (now - lastSeenUtc < TimeSpan.FromSeconds(MissingBeaconGraceSec))
                continue;

            if (shouldLogMissing)
            {
                AddLog($"Trusted device \"{trustedDevice.Name}\" stopped beaconing for {MissingBeaconGraceSec}s; treating as out of range.");
            }

            _proximityMonitor.UpdateRssi(bluetoothAddress, OutOfRangeThreshold - 1);
        }
    }

    partial void OnIsDeviceScanActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(DeviceScanButtonText));
    }

    partial void OnIsExecutionPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExecutionPauseButtonText));
        _trayManager.SetExecutionPaused(value);
        AddLog(value
            ? "Execution paused. Out-of-range events will be logged without running the command."
            : "Execution resumed.");
    }

    [RelayCommand]
    private void StartDeviceScan()
    {
        _deviceScanSecondsRemaining = ManualDeviceScanDurationSec;
        IsDeviceScanActive = true;
        DeviceScanStatusText = $"Scanning nearby BLE devices for {_deviceScanSecondsRemaining}s...";
        _bleScanner.StartDiscovery(TimeSpan.FromSeconds(ManualDeviceScanDurationSec));
        _deviceScanTimer.Start();
        AddLog("Manual device discovery started.");
        OnPropertyChanged(nameof(DeviceScanButtonText));
    }

    [RelayCommand]
    private void StopDeviceScan()
    {
        _deviceScanTimer.Stop();
        _bleScanner.StopDiscovery();
        IsDeviceScanActive = false;
        _deviceScanSecondsRemaining = 0;
        DeviceScanStatusText = "Monitoring trusted devices only.";
        AddLog("Manual device discovery stopped.");
        OnPropertyChanged(nameof(DeviceScanButtonText));
    }

    [RelayCommand]
    private void ToggleExecutionPause()
    {
        IsExecutionPaused = !IsExecutionPaused;
    }

    [RelayCommand]
    private void MinimizeApplication()
    {
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow == null)
            return;

        mainWindow.ShowInTaskbar = false;
        mainWindow.Hide();
        _trayManager.ShowBalloonTip(
            "BLE Proximity",
            "The application is still running in the system tray. Right-click the tray icon for options.");
    }

    [RelayCommand]
    private void ExitApplication()
    {
        if (Application.Current != null)
        {
            Application.Current.Properties["ExplicitExitRequested"] = true;
        }

        Application.Current?.Shutdown();
    }

    private void OnTrayExecutionPauseChanged(object? sender, bool isPaused)
    {
        RunOnUiThread(() => IsExecutionPaused = isPaused);
    }

    private void OnTrayExitRequested(object? sender, EventArgs e)
    {
        RunOnUiThread(() => Application.Current?.Shutdown());
    }

    private void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.InvokeAsync(action).Task.ContinueWith(task =>
        {
            Console.WriteLine($"[MainViewModel] EXCEPTION in dispatched UI state update: {task.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private void ProcessProximityStateChangedOnUiThread(ProximityState oldState, ProximityState newState)
    {
        Console.WriteLine($"[MainViewModel] Updating UI for state: {newState}");

        CurrentProximityState = newState;
        UpdateStatusText(newState);

        var activeDevice = GetActiveDeviceName();
        var activeRssi = GetActiveDeviceRssi();

        try
        {
            _trayManager.UpdateState(newState, activeDevice, activeRssi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] WARNING: Tray update failed: {ex}");
        }

        Console.WriteLine($"[MainViewModel] Entering UI switch for state: {newState}");
        switch (newState)
        {
            case ProximityState.OutOfRangePending:
                Console.WriteLine("[MainViewModel] Processing OutOfRangePending case");
                AddLog($"Device \"{activeDevice}\" signal dropped below threshold, waiting for timeout...");
                break;

            case ProximityState.Countdown:
                Console.WriteLine("[MainViewModel] Processing Countdown case");
                if (IsExecutionPaused)
                {
                    AddLog($"Device \"{activeDevice}\" is out of range. Execution is paused; command will not run.");
                    ShowTrayNotification(
                        "BLE Proximity",
                        $"Device \"{activeDevice ?? "Unknown"}\" is out of range. Execution is paused.");
                }
                else
                {
                    var commandDescription = GetCommandDescription();
                    var deviceName = activeDevice ?? "Unknown";
                    _toastNotifier.ShowCountdownToast(deviceName, commandDescription, 3);
                    ShowTrayNotification(
                        "BLE Proximity Warning",
                        $"Device \"{deviceName}\" is out of range. Executing {commandDescription} in 3 seconds.");
                    AddLog($"Device \"{activeDevice}\" out of range, timeout exceeded. Countdown started.");
                }
                break;

            case ProximityState.Executing:
                Console.WriteLine("[MainViewModel] Processing Executing UI case");
                if (IsExecutionPaused)
                    AddLog($"Command skipped because execution is paused. Device \"{activeDevice}\" remains out of range.");
                else
                    AddLog($"Executing command: {GetCommandDescription()}");
                break;

            case ProximityState.OutOfRangeLatched:
                Console.WriteLine("[MainViewModel] Processing OutOfRangeLatched UI case");
                if (oldState == ProximityState.Executing)
                    AddLog($"Out-of-range action completed for \"{activeDevice}\". Waiting for device to return in range before triggering again.");
                break;

            case ProximityState.InRange when oldState == ProximityState.Cancelled:
                _toastNotifier.DismissCountdownToast();
                AddLog($"Device \"{activeDevice}\" returned to range. Countdown cancelled.");
                break;

            case ProximityState.InRange:
                if (oldState == ProximityState.Executing)
                    AddLog($"Command completed. Resumed monitoring.");
                else if (oldState == ProximityState.OutOfRangeLatched)
                    AddLog($"Device \"{activeDevice}\" returned to range. Out-of-range trigger reset.");
                else if (oldState != ProximityState.InRange)
                    AddLog($"Device \"{activeDevice}\" is in range.");
                break;

            case ProximityState.Cancelled:
                _toastNotifier.DismissCountdownToast();
                break;

            default:
                Console.WriteLine($"[MainViewModel] WARNING: Unhandled state in UI switch: {newState}");
                break;
        }

        Console.WriteLine("[MainViewModel] UI state update completed successfully");
    }

    private void StartCommandExecutionTask()
    {
        Console.WriteLine("[MainViewModel] Processing Executing case - COMMAND SHOULD EXECUTE NOW");

        if (IsExecutionPaused)
        {
            Console.WriteLine("[MainViewModel] Execution paused, skipping command");
            _ = Task.Run(() =>
            {
                NotifyCommandCompleted();
            });
            return;
        }

        Console.WriteLine("[MainViewModel] State is Executing, starting command execution task");

        var commandConfig = GetCurrentCommandConfig();
        var context = CreateDeviceContextSnapshot();

        _ = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine("[MainViewModel] Command execution task started");
                await ExecuteCommandInternalAsync(commandConfig, context);
                Console.WriteLine("[MainViewModel] Command execution task completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainViewModel] Command execution task failed: {ex}");
                AddLog($"Command failed: {ex.Message}");
            }
            finally
            {
                NotifyCommandCompleted();
            }
        });
    }

    private void UpdateStatusText(ProximityState state)
    {
        if (TrustedDevices.Count == 0)
        {
            StatusText = "Idle — No trusted device configured";
            return;
        }

        var deviceInfo = string.Join(", ", TrustedDevices.Select(td =>
        {
            var scanned = ScannedDevices.FirstOrDefault(d => d.MacAddress == td.MacAddress);
            var rssiStr = scanned != null ? $"{scanned.SmoothedRssi:F0} dBm" : "not seen";
            return $"{td.Name} ({rssiStr})";
        }));

        var stateStr = state switch
        {
            ProximityState.InRange => "In Range ✓",
            ProximityState.OutOfRangePending => "Out of Range Pending ⏳",
            ProximityState.Countdown => "Countdown ⚠️ — Executing in 3s",
            ProximityState.Executing => "Executing Command 🔒",
            ProximityState.OutOfRangeLatched => "Out of Range — Waiting for return",
            ProximityState.Cancelled => "Cancelled — Returned to range",
            _ => state.ToString()
        };

        StatusText = $"{stateStr} | Devices: {deviceInfo}";
    }

    private void ShowTrayNotification(string title, string message)
    {
        try
        {
            _trayManager.ShowBalloonTip(title, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] WARNING: Tray notification failed: {ex}");
        }
    }

    private void AddLog(string message)
    {
        var entry = $"{DateTime.Now:HH:mm:ss} {message}";
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LogEntries.Add(entry);
            // Keep max 200 entries
            while (LogEntries.Count > 200)
                LogEntries.RemoveAt(0);
        });
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        ApplyTheme(value);
    }

    public void ApplyCurrentTheme()
    {
        ApplyTheme(IsDarkMode);
    }

    private static void ApplyTheme(bool dark)
    {
        var app = Application.Current;
        if (app?.MainWindow == null) return;

        var window = app.MainWindow;

        if (dark)
        {
            SetThemeBrush(window, "AppBackgroundBrush", 17, 24, 39);
            SetThemeBrush(window, "PanelBrush", 24, 33, 49);
            SetThemeBrush(window, "PanelBorderBrush", 55, 65, 81);
            SetThemeBrush(window, "PrimaryTextBrush", 241, 245, 249);
            SetThemeBrush(window, "TextMutedBrush", 148, 163, 184);
            SetThemeBrush(window, "SubtleBackgroundBrush", 31, 41, 55);
            SetThemeBrush(window, "InputBackgroundBrush", 15, 23, 42);
            SetThemeBrush(window, "ButtonBackgroundBrush", 30, 41, 59);
            SetThemeBrush(window, "ButtonBorderBrush", 71, 85, 105);
            SetThemeBrush(window, "GridLineBrush", 51, 65, 85);
            SetThemeBrush(window, "AccentBrush", 59, 130, 246);
            SetThemeBrush(window, "AccentDarkBrush", 37, 99, 235);
            SetThemeBrush(window, "DangerTextBrush", 248, 113, 113);
            SetThemeBrush(window, "SelectionBrush", 30, 64, 115);
        }
        else
        {
            SetThemeBrush(window, "AppBackgroundBrush", 244, 246, 248);
            SetThemeBrush(window, "PanelBrush", 255, 255, 255);
            SetThemeBrush(window, "PanelBorderBrush", 220, 226, 234);
            SetThemeBrush(window, "PrimaryTextBrush", 24, 34, 49);
            SetThemeBrush(window, "TextMutedBrush", 101, 116, 135);
            SetThemeBrush(window, "SubtleBackgroundBrush", 248, 250, 252);
            SetThemeBrush(window, "InputBackgroundBrush", 255, 255, 255);
            SetThemeBrush(window, "ButtonBackgroundBrush", 255, 255, 255);
            SetThemeBrush(window, "ButtonBorderBrush", 200, 210, 222);
            SetThemeBrush(window, "GridLineBrush", 238, 242, 246);
            SetThemeBrush(window, "AccentBrush", 37, 99, 235);
            SetThemeBrush(window, "AccentDarkBrush", 29, 78, 216);
            SetThemeBrush(window, "DangerTextBrush", 185, 28, 28);
            SetThemeBrush(window, "SelectionBrush", 219, 234, 254);
        }

        window.Background = (System.Windows.Media.Brush)window.Resources["AppBackgroundBrush"];
        window.Foreground = (System.Windows.Media.Brush)window.Resources["PrimaryTextBrush"];
        window.Resources[SystemColors.WindowBrushKey] = window.Resources["AppBackgroundBrush"];
        window.Resources[SystemColors.WindowTextBrushKey] = window.Resources["PrimaryTextBrush"];
        window.Resources[SystemColors.ControlBrushKey] = window.Resources["ButtonBackgroundBrush"];
        window.Resources[SystemColors.ControlTextBrushKey] = window.Resources["PrimaryTextBrush"];
        window.Resources[SystemColors.HighlightBrushKey] = window.Resources["AccentBrush"];
        window.Resources[SystemColors.HighlightTextBrushKey] = System.Windows.Media.Brushes.White;
    }

    private static void SetThemeBrush(Window window, string key, byte red, byte green, byte blue)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
        brush.Freeze();
        window.Resources[key] = brush;
    }

    private async Task ExecuteCommandInternalAsync(CommandConfig commandConfig, DeviceContext context)
    {
        Console.WriteLine("[MainViewModel] ExecuteCommandInternalAsync started");
        Console.WriteLine($"[MainViewModel] Command config: {commandConfig.ExecutablePath} {commandConfig.Arguments}");

        try
        {
            Console.WriteLine("[MainViewModel] Calling CommandExecutor.ExecuteAsync");
            await _commandExecutor.ExecuteAsync(commandConfig, context);
            Console.WriteLine("[MainViewModel] CommandExecutor.ExecuteAsync completed successfully");
            AddLog("Command executed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] CommandExecutor.ExecuteAsync failed: {ex}");
            AddLog($"Command failed: {ex.Message}");
        }
    }

    private DeviceContext CreateDeviceContextSnapshot()
    {
        try
        {
            return new DeviceContext
            {
                MacAddress = GetActiveDeviceMac() ?? string.Empty,
                Name = GetActiveDeviceName() ?? "Unknown",
                SmoothedRssi = GetActiveDeviceRssi() ?? 0,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] WARNING: Failed to create device context snapshot: {ex}");
            return new DeviceContext
            {
                Name = "Unknown",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private void NotifyCommandCompleted()
    {
        // NotifyCommandCompleted must be called OUTSIDE the StateChanged event handler
        // to avoid deadlock (StateChanged fires inside the ProximityMonitor's lock)
        Console.WriteLine("[MainViewModel] Calling NotifyCommandCompleted");
        if (_proximityMonitor is ProximityMonitor pm)
        {
            pm.NotifyCommandCompleted();
        }
        else
        {
            Console.WriteLine("[MainViewModel] WARNING: _proximityMonitor is not ProximityMonitor type");
        }
    }

    private CommandConfig GetCurrentCommandConfig()
    {
        if (CommandPresets.Presets.TryGetValue(CommandPreset, out var preset) && CommandPreset != "CustomScript")
        {
            return preset;
        }

        return new CommandConfig(ExecutablePath, Arguments);
    }

    private string GetCommandDescription()
    {
        if (CommandPreset != "CustomScript" && CommandPresets.Presets.ContainsKey(CommandPreset))
        {
            return CommandPreset;
        }

        return $"{ExecutablePath} {Arguments}".Trim();
    }

    private string? GetActiveDeviceName()
    {
        if (!UseMultiDevice && TrustedDevices.Count > 0)
        {
            return TrustedDevices[0].Name;
        }

        // In multi-device mode, return the first trusted device name
        return TrustedDevices.FirstOrDefault()?.Name;
    }

    private string? GetActiveDeviceMac()
    {
        return TrustedDevices.FirstOrDefault()?.MacAddress;
    }

    private void SyncTrustedDevicesWithScanner()
    {
        _bleScanner.SetTrustedDevices(TrustedDevices);

        var trustedMacs = TrustedDevices
            .Select(device => device.MacAddress)
            .Where(mac => !string.IsNullOrWhiteSpace(mac))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_trustedSignalLock)
        {
            foreach (var macAddress in _trustedDeviceLastSeenUtc.Keys.ToList())
            {
                if (!trustedMacs.Contains(macAddress))
                {
                    _trustedDeviceLastSeenUtc.Remove(macAddress);
                    _trustedDeviceBluetoothAddresses.Remove(macAddress);
                    _trustedDeviceMissingAlerts.Remove(macAddress);
                }
            }
        }
    }

    private double? GetActiveDeviceRssi()
    {
        var mac = GetActiveDeviceMac();
        if (mac == null) return null;

        var device = ScannedDevices.FirstOrDefault(d => d.MacAddress == mac);
        return device?.SmoothedRssi;
    }

    [RelayCommand]
    private void AddTrustedDevice(ScannedDevice? device)
    {
        if (device == null) return;

        // Check if device already in trusted list
        if (TrustedDevices.Any(td => td.MacAddress.Equals(device.MacAddress, StringComparison.OrdinalIgnoreCase)))
        {
            _notificationService.ShowWarning(
                $"Device '{device.Name}' ({device.MacAddress}) is already in the trusted device list.",
                "Device Already Trusted");
            return;
        }

        // Enforce max 10 devices
        if (TrustedDevices.Count >= 10)
        {
            _notificationService.ShowWarning(
                "Maximum of 10 trusted devices reached. Remove a device before adding a new one.",
                "Trusted Device Limit");
            return;
        }

        TrustedDevices.Add(new TrustedDevice
        {
            Name = device.Name,
            BluetoothAddress = device.BluetoothAddress,
            MacAddress = device.MacAddress
        });
        lock (_trustedSignalLock)
        {
            _trustedDeviceLastSeenUtc[device.MacAddress] = device.LastSeen == default
                ? DateTime.UtcNow
                : device.LastSeen;
            _trustedDeviceBluetoothAddresses[device.MacAddress] = device.BluetoothAddress;
            _trustedDeviceMissingAlerts.Remove(device.MacAddress);
        }
        SyncTrustedDevicesWithScanner();
    }

    [RelayCommand]
    private void RemoveTrustedDevice(TrustedDevice? device)
    {
        if (device == null) return;
        TrustedDevices.Remove(device);
        SyncTrustedDevicesWithScanner();
    }

    [RelayCommand]
    private void RenameTrustedDevice(TrustedDevice? device)
    {
        if (device == null) return;

        // Show input dialog for new name
        var newName = _notificationService.ShowInputDialog(
            "Rename Device", 
            "Enter new name for the device:", 
            device.Name);

        if (!string.IsNullOrWhiteSpace(newName) && newName != device.Name)
        {
            device.Name = newName;
            
            // Save configuration immediately
            SaveConfiguration();
            
            AddLog($"Device renamed to \"{newName}\"");
        }
    }

    private void SaveConfiguration()
    {
        var config = new AppConfig
        {
            StartWithWindows = StartWithWindows,
            UseMultiDevice = UseMultiDevice,
            DarkMode = IsDarkMode,
            SingleTargetMac = TrustedDevices.FirstOrDefault()?.MacAddress,
            TrustedDevices = TrustedDevices.Select(td => new TrustedDeviceConfig
            {
                Name = td.Name,
                MacAddress = td.MacAddress
            }).ToList(),
            InRangeThreshold = InRangeThreshold,
            OutOfRangeThreshold = OutOfRangeThreshold,
            OutOfRangeTimeoutSec = OutOfRangeTimeoutSec,
            RssiAlpha = RssiAlpha,
            CommandPreset = CommandPreset,
            CustomCommand = CommandPreset == "CustomScript"
                ? new CommandConfig(ExecutablePath, Arguments)
                : null,
            GracePeriodSec = GracePeriodSec,
            MissingBeaconGraceSec = Math.Clamp(MissingBeaconGraceSec, 1, 30)
        };

        _configManager.Save(config);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        ValidationError = null;

        // Validate thresholds
        var thresholdResult = ThresholdValidator.Validate(InRangeThreshold, OutOfRangeThreshold);
        if (!thresholdResult.IsValid)
        {
            ValidationError = thresholdResult.ErrorMessage;
            _notificationService.ShowError(thresholdResult.ErrorMessage!, "Validation Error");
            return;
        }

        // Validate custom command executable path
        if (CommandPreset == "CustomScript" && !string.IsNullOrWhiteSpace(ExecutablePath))
        {
            try
            {
                if (!File.Exists(ExecutablePath))
                {
                    ValidationError = $"Executable path not found: {ExecutablePath}";
                    _notificationService.ShowError(ValidationError, "Validation Error");
                    return;
                }
            }
            catch
            {
                // File system unavailability - treat as invalid
                ValidationError = $"Unable to validate executable path: {ExecutablePath}";
                _notificationService.ShowError(ValidationError, "Validation Error");
                return;
            }
        }

        // Build config
        var config = new AppConfig
        {
            StartWithWindows = StartWithWindows,
            UseMultiDevice = UseMultiDevice,
            DarkMode = IsDarkMode,
            SingleTargetMac = TrustedDevices.FirstOrDefault()?.MacAddress,
            TrustedDevices = TrustedDevices.Select(td => new TrustedDeviceConfig
            {
                Name = td.Name,
                MacAddress = td.MacAddress
            }).ToList(),
            InRangeThreshold = InRangeThreshold,
            OutOfRangeThreshold = OutOfRangeThreshold,
            OutOfRangeTimeoutSec = OutOfRangeTimeoutSec,
            RssiAlpha = RssiAlpha,
            CommandPreset = CommandPreset,
            CustomCommand = CommandPreset == "CustomScript"
                ? new CommandConfig(ExecutablePath, Arguments)
                : null,
            GracePeriodSec = GracePeriodSec,
            MissingBeaconGraceSec = Math.Clamp(MissingBeaconGraceSec, 1, 30)
        };

        _configManager.Save(config);

        // Reconfigure proximity monitor with new settings
        _proximityMonitor.Configure(new ProximityConfig
        {
            InRangeThreshold = InRangeThreshold,
            OutOfRangeThreshold = OutOfRangeThreshold,
            OutOfRangeTimeoutSec = OutOfRangeTimeoutSec,
            GracePeriodSec = GracePeriodSec
        });

        // Update smoother alpha
        _rssiSmoother.Alpha = RssiAlpha;
        MissingBeaconGraceSec = Math.Clamp(MissingBeaconGraceSec, 1, 30);
        SyncTrustedDevicesWithScanner();

        ValidationError = null;
    }

    [RelayCommand]
    private void ToggleStartup()
    {
        var success = _startupManager.SetStartupEnabled(StartWithWindows);
        if (!success)
        {
            // Revert the toggle
            StartWithWindows = !StartWithWindows;
        }
    }

    [RelayCommand]
    private void ToggleMultiDevice()
    {
        // UseMultiDevice property is already toggled by the binding
        // If switching to single device mode and we have multiple trusted devices,
        // keep only the first one
        if (!UseMultiDevice && TrustedDevices.Count > 1)
        {
            var first = TrustedDevices[0];
            TrustedDevices.Clear();
            TrustedDevices.Add(first);
            SyncTrustedDevicesWithScanner();
        }
    }
}
