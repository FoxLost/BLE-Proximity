using System.Windows;
using System.Windows.Threading;
using BLEProximity.Models;
using BLEProximity.Services;
using BLEProximity.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;
using System.Runtime.InteropServices;
using System.IO;

namespace BLEProximity;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool FreeConsole();

    private SingleInstanceManager? _singleInstanceManager;
    private IBleScanner? _bleScanner;
    private IProximityMonitor? _proximityMonitor;
    private TaskbarIcon? _taskbarIcon;
    private WindowBehaviorManager? _windowBehaviorManager;
    private bool _debugLoggingEnabled;

    protected override void OnStartup(StartupEventArgs e)
    {
        _debugLoggingEnabled = e.Args.Any(arg => arg.Equals("--debug-console", StringComparison.OrdinalIgnoreCase))
            || Environment.GetEnvironmentVariable("BLELOCK_DEBUG") == "1";

        if (_debugLoggingEnabled)
        {
            AllocConsole();
            WriteStartupDebugLog("=== BLE Proximity App Debug Console ===");
            WriteStartupDebugLog($"Started at: {DateTime.Now}");
        }

        base.OnStartup(e);

        try
        {
            WriteStartupDebugLog("Starting single-instance check...");
            
            // Step 1: Single-instance check (Mutex) as first operation
            _singleInstanceManager = new SingleInstanceManager();
        try
        {
            bool acquired = _singleInstanceManager.TryAcquire();
            if (!acquired)
            {
                // Another instance is running - send restore message and terminate
                _singleInstanceManager.SendRestoreMessage();
                _singleInstanceManager.Dispose();
                _singleInstanceManager = null;
                Shutdown();
                return;
            }
        }
        catch (SingleInstanceException ex)
        {
            // Mutex acquisition failed due to permissions or system resource issues
            MessageBox.Show(
                ex.Message,
                "BLE Proximity - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _singleInstanceManager?.Dispose();
            _singleInstanceManager = null;
            Shutdown();
            return;
        }

        // Step 2: Load configuration via ConfigManager (handles corrupt file with modal notification)
        var notificationService = new MessageBoxNotificationService();
        var configManager = new ConfigManager();
        var config = configManager.Load();

        // Step 3: Initialize ShortcutInstaller (ensure toast shortcut exists)
        var shortcutInstaller = new ShortcutInstaller();
        shortcutInstaller.EnsureShortcutExists();

        // Step 4: Create all services with manual composition
        var rssiSmoother = new RssiSmoother();
        rssiSmoother.Alpha = config.RssiAlpha;

        _bleScanner = new BleScanner(rssiSmoother, notificationService, Dispatcher.CurrentDispatcher);

        var proximityMonitor = new ProximityMonitor();
        proximityMonitor.Configure(new ProximityConfig
        {
            InRangeThreshold = config.InRangeThreshold,
            OutOfRangeThreshold = config.OutOfRangeThreshold,
            OutOfRangeTimeoutSec = config.OutOfRangeTimeoutSec,
            GracePeriodSec = config.GracePeriodSec
        });
        _proximityMonitor = proximityMonitor;

        var multiDevicePolicy = new MultiDevicePolicy(
            config.InRangeThreshold,
            config.OutOfRangeThreshold,
            config.OutOfRangeTimeoutSec,
            !config.UseMultiDevice);
        var toastNotifier = new ToastNotifier();
        var commandExecutor = new CommandExecutor(toastNotifier);
        var trayManager = new TrayManager();
        var startupManager = new StartupManager();

        // Step 5: Create MainViewModel with all services
        var viewModel = new MainViewModel(
            _bleScanner,
            rssiSmoother,
            _proximityMonitor,
            toastNotifier,
            commandExecutor,
            configManager,
            startupManager,
            trayManager,
            notificationService);

        // Step 6: Create MainWindow and set DataContext
        var mainWindow = new MainWindow();
        mainWindow.DataContext = viewModel;
        MainWindow = mainWindow;
        viewModel.ApplyCurrentTheme();

        // Step 7: Start BLE scanner
        _bleScanner.Start();

        // Step 8: Start Proximity Monitor (with grace period before activating monitoring)
        _proximityMonitor.Start();

        // Step 9: Initialize TrayManager with TaskbarIcon, display tray icon, and hide Main_Window
        _taskbarIcon = new TaskbarIcon();
        // Use the application's embedded icon for the tray
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                _taskbarIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            }
        }
        catch { }
        _taskbarIcon.ToolTipText = "BLE Proximity";
        trayManager.Initialize(_taskbarIcon);

        // Step 10: Wire WindowBehaviorManager
        _windowBehaviorManager = new WindowBehaviorManager(trayManager, _bleScanner);
        mainWindow.AttachWindowBehavior(_windowBehaviorManager);
        trayManager.SetWindowBehaviorManager(_windowBehaviorManager);

        // Step 11: Start SingleInstanceManager listening for restore messages
        _singleInstanceManager.StartListening(() =>
        {
            // Marshal to UI thread to restore the window
            Dispatcher.InvokeAsync(() =>
            {
                _windowBehaviorManager?.RestoreFromTray();
            });
        });
        
        WriteStartupDebugLog("Startup completed successfully!");
        }
        catch (Exception ex)
        {
            WriteStartupDebugLog($"STARTUP ERROR: {ex}");
            MessageBox.Show($"Startup failed: {ex.Message}", "BLE Proximity - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Shutdown sequence: stop scanner, dispose services, remove tray icon, terminate

        // Dispose SingleInstanceManager (releases mutex and stops pipe listener)
        _singleInstanceManager?.Dispose();
        _singleInstanceManager = null;

        // Stop BLE scanner and dispose
        _bleScanner?.Stop();
        _bleScanner?.Dispose();
        _bleScanner = null;

        // Stop and dispose proximity monitor
        _proximityMonitor?.Stop();
        if (_proximityMonitor is IDisposable disposableMonitor)
        {
            disposableMonitor.Dispose();
        }
        _proximityMonitor = null;

        // Detach window behavior manager
        _windowBehaviorManager?.Detach();
        _windowBehaviorManager = null;

        // Remove tray icon
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;

        base.OnExit(e);
    }

    private void WriteStartupDebugLog(string message)
    {
        if (!_debugLoggingEnabled)
            return;

        Console.WriteLine(message);

        try
        {
            File.AppendAllText("startup_debug.log", message + Environment.NewLine);
        }
        catch
        {
            // Startup debug logging is best-effort only.
        }
    }
}
