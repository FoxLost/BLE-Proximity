using System.IO;
using System.Text.Json;
using BLEProximity.Models;

namespace BLEProximity.Services;

public class ConfigManager : IConfigManager
{
    private static readonly string DefaultConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BLE Proximity");

    private static readonly string DefaultConfigFilePath = Path.Combine(DefaultConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly INotificationService _notificationService;

    private AppConfig _currentConfig = new();

    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    /// <summary>
    /// Creates a ConfigManager with default paths and MessageBox notifications.
    /// </summary>
    public ConfigManager()
        : this(DefaultConfigDirectory, DefaultConfigFilePath, new MessageBoxNotificationService())
    {
    }

    /// <summary>
    /// Creates a ConfigManager with configurable paths and notification service for testability.
    /// </summary>
    public ConfigManager(string configDirectory, string configFilePath, INotificationService notificationService)
    {
        _configDirectory = configDirectory;
        _configFilePath = configFilePath;
        _notificationService = notificationService;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configFilePath))
        {
            _currentConfig = CreateDefaultConfig();
            EnsureDirectoryExists();
            WriteConfigToFile(_currentConfig);
            return _currentConfig;
        }

        try
        {
            var json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

            if (config == null)
            {
                return HandleMalformedConfig();
            }

            NormalizeConfig(config);
            _currentConfig = config;
            return _currentConfig;
        }
        catch (JsonException)
        {
            return HandleMalformedConfig();
        }
        catch (IOException)
        {
            return HandleMalformedConfig();
        }
    }

    public void Save(AppConfig config)
    {
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configFilePath, json);
            _currentConfig = config;
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs { Config = config });
        }
        catch (IOException ex)
        {
            // Retain in-memory config, display error message indicating save failed
            _notificationService.ShowError(
                $"Failed to save configuration: {ex.Message}\n\nYour settings are retained in memory but could not be written to disk.",
                "Configuration Save Error");
        }
        catch (UnauthorizedAccessException ex)
        {
            // Retain in-memory config, display error message indicating save failed
            _notificationService.ShowError(
                $"Failed to save configuration: {ex.Message}\n\nYour settings are retained in memory but could not be written to disk.",
                "Configuration Save Error");
        }
    }

    /// <summary>
    /// Gets the current in-memory configuration.
    /// </summary>
    public AppConfig CurrentConfig => _currentConfig;

    private AppConfig HandleMalformedConfig()
    {
        // Rename corrupt file to .bak
        try
        {
            var backupPath = _configFilePath + ".bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            File.Move(_configFilePath, backupPath);
        }
        catch
        {
            // Best effort to rename; if it fails, proceed with creating defaults
        }

        // Create fresh default config
        _currentConfig = CreateDefaultConfig();
        EnsureDirectoryExists();
        WriteConfigToFile(_currentConfig);

        // Pause startup until user acknowledges modal notification
        _notificationService.ShowWarning(
            "The configuration file was corrupted and has been reset to default values.\n\nThe corrupt file has been renamed with a .bak extension.",
            "Configuration Reset");

        return _currentConfig;
    }

    private static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            StartWithWindows = false,
            UseMultiDevice = false,
            DarkMode = false,
            SingleTargetMac = null,
            TrustedDevices = new List<TrustedDeviceConfig>(),
            InRangeThreshold = -70,
            OutOfRangeThreshold = -75,
            OutOfRangeTimeoutSec = 10,
            RssiAlpha = 0.3,
            CommandPreset = "LockWorkstation",
            CustomCommand = null,
            GracePeriodSec = 5,
            MissingBeaconGraceSec = 3
        };
    }

    private static void NormalizeConfig(AppConfig config)
    {
        if (config.MissingBeaconGraceSec <= 0)
        {
            config.MissingBeaconGraceSec = 3;
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }

    private void WriteConfigToFile(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch
        {
            // If we can't write the default config, proceed with in-memory defaults
        }
    }
}
