using System.IO;
using System.Text.Json;
using BLEProximity.Models;
using BLEProximity.Services;

namespace BLEProximity.Tests.Integration;

/// <summary>
/// Integration tests for configuration persistence lifecycle.
/// Validates: Requirements 11.4, 11.5
/// Tests config file creation and loading on startup, and save/load round-trip.
/// </summary>
public class ConfigPersistence_Tests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _configFilePath;
    private readonly FakeNotificationService _notificationService;

    public ConfigPersistence_Tests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "BLEProximity_IntegrationTests_" + Guid.NewGuid().ToString("N"));
        _configFilePath = Path.Combine(_tempDirectory, "config.json");
        _notificationService = new FakeNotificationService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private ConfigManager CreateConfigManager()
    {
        return new ConfigManager(_tempDirectory, _configFilePath, _notificationService);
    }

    #region Loading from non-existent file creates default config

    [Fact]
    public void Load_NonExistentFile_CreatesDefaultConfig()
    {
        // Arrange: no directory or file exists
        Assert.False(Directory.Exists(_tempDirectory));
        Assert.False(File.Exists(_configFilePath));

        var manager = CreateConfigManager();

        // Act
        var config = manager.Load();

        // Assert: default config is created on disk and returned
        Assert.True(File.Exists(_configFilePath));
        Assert.False(config.StartWithWindows);
        Assert.False(config.UseMultiDevice);
        Assert.False(config.DarkMode);
        Assert.Null(config.SingleTargetMac);
        Assert.Empty(config.TrustedDevices);
        Assert.Equal(-70, config.InRangeThreshold);
        Assert.Equal(-75, config.OutOfRangeThreshold);
        Assert.Equal(10, config.OutOfRangeTimeoutSec);
        Assert.Equal(0.3, config.RssiAlpha);
        Assert.Equal("LockWorkstation", config.CommandPreset);
        Assert.Null(config.CustomCommand);
        Assert.Equal(5, config.GracePeriodSec);
    }

    [Fact]
    public void Load_NonExistentFile_CreatedFileIsValidJson()
    {
        // Arrange
        var manager = CreateConfigManager();

        // Act
        manager.Load();

        // Assert: the created file is valid JSON that can be deserialized
        var json = File.ReadAllText(_configFilePath);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json, options);
        Assert.NotNull(deserialized);
        Assert.Equal(-70, deserialized!.InRangeThreshold);
    }

    #endregion

    #region Loading from a valid file returns correct values

    [Fact]
    public void Load_ValidFile_ReturnsCorrectValues()
    {
        // Arrange: write a valid config file with custom values
        Directory.CreateDirectory(_tempDirectory);
        var customConfig = new AppConfig
        {
            StartWithWindows = true,
            UseMultiDevice = true,
            DarkMode = true,
            SingleTargetMac = "AABBCCDDEEFF",
            TrustedDevices = new List<TrustedDeviceConfig>
            {
                new() { Name = "Phone", MacAddress = "112233445566" },
                new() { Name = "Watch", MacAddress = "AABBCCDDEEFF" }
            },
            InRangeThreshold = -60,
            OutOfRangeThreshold = -70,
            OutOfRangeTimeoutSec = 20,
            RssiAlpha = 0.4,
            CommandPreset = "MuteVolume",
            CustomCommand = new CommandConfig("powershell.exe", "-Command \"Start-Sleep 1\""),
            GracePeriodSec = 10,
            MissingBeaconGraceSec = 4
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(customConfig, options));

        var manager = CreateConfigManager();

        // Act
        var loaded = manager.Load();

        // Assert: all fields match what was written
        Assert.True(loaded.StartWithWindows);
        Assert.True(loaded.UseMultiDevice);
        Assert.True(loaded.DarkMode);
        Assert.Equal("AABBCCDDEEFF", loaded.SingleTargetMac);
        Assert.Equal(2, loaded.TrustedDevices.Count);
        Assert.Equal("Phone", loaded.TrustedDevices[0].Name);
        Assert.Equal("112233445566", loaded.TrustedDevices[0].MacAddress);
        Assert.Equal("Watch", loaded.TrustedDevices[1].Name);
        Assert.Equal("AABBCCDDEEFF", loaded.TrustedDevices[1].MacAddress);
        Assert.Equal(-60, loaded.InRangeThreshold);
        Assert.Equal(-70, loaded.OutOfRangeThreshold);
        Assert.Equal(20, loaded.OutOfRangeTimeoutSec);
        Assert.Equal(0.4, loaded.RssiAlpha);
        Assert.Equal("MuteVolume", loaded.CommandPreset);
        Assert.NotNull(loaded.CustomCommand);
        Assert.Equal("powershell.exe", loaded.CustomCommand!.ExecutablePath);
        Assert.Equal("-Command \"Start-Sleep 1\"", loaded.CustomCommand.Arguments);
        Assert.Equal(10, loaded.GracePeriodSec);
        Assert.Equal(4, loaded.MissingBeaconGraceSec);
    }

    [Fact]
    public void Load_ValidFile_NoNotificationsShown()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(new AppConfig(), options));

        var manager = CreateConfigManager();

        // Act
        manager.Load();

        // Assert: no warnings or errors for valid file
        Assert.Empty(_notificationService.WarningMessages);
        Assert.Empty(_notificationService.ErrorMessages);
    }

    #endregion

    #region Save then Load preserves all fields (round-trip)

    [Fact]
    public void SaveThenLoad_PreservesAllFields()
    {
        // Arrange
        var manager = CreateConfigManager();
        var config = new AppConfig
        {
            StartWithWindows = true,
            UseMultiDevice = true,
            DarkMode = true,
            SingleTargetMac = "FFEEDDCCBBAA",
            TrustedDevices = new List<TrustedDeviceConfig>
            {
                new() { Name = "Laptop", MacAddress = "001122334455" },
                new() { Name = "Tablet", MacAddress = "AABBCCDDEEFF" },
                new() { Name = "Headphones", MacAddress = "667788990011" }
            },
            InRangeThreshold = -55,
            OutOfRangeThreshold = -65,
            OutOfRangeTimeoutSec = 30,
            RssiAlpha = 0.15,
            CommandPreset = "CustomScript",
            CustomCommand = new CommandConfig("C:\\Scripts\\lock.bat", "--mac {mac} --name {name}"),
            GracePeriodSec = 15
        };

        // Act: save then create a new manager and load
        manager.Save(config);

        var manager2 = CreateConfigManager();
        var loaded = manager2.Load();

        // Assert: all fields preserved
        Assert.Equal(config.StartWithWindows, loaded.StartWithWindows);
        Assert.Equal(config.UseMultiDevice, loaded.UseMultiDevice);
        Assert.Equal(config.DarkMode, loaded.DarkMode);
        Assert.Equal(config.SingleTargetMac, loaded.SingleTargetMac);
        Assert.Equal(config.TrustedDevices.Count, loaded.TrustedDevices.Count);
        for (int i = 0; i < config.TrustedDevices.Count; i++)
        {
            Assert.Equal(config.TrustedDevices[i].Name, loaded.TrustedDevices[i].Name);
            Assert.Equal(config.TrustedDevices[i].MacAddress, loaded.TrustedDevices[i].MacAddress);
        }
        Assert.Equal(config.InRangeThreshold, loaded.InRangeThreshold);
        Assert.Equal(config.OutOfRangeThreshold, loaded.OutOfRangeThreshold);
        Assert.Equal(config.OutOfRangeTimeoutSec, loaded.OutOfRangeTimeoutSec);
        Assert.Equal(config.RssiAlpha, loaded.RssiAlpha);
        Assert.Equal(config.CommandPreset, loaded.CommandPreset);
        Assert.NotNull(loaded.CustomCommand);
        Assert.Equal(config.CustomCommand.ExecutablePath, loaded.CustomCommand!.ExecutablePath);
        Assert.Equal(config.CustomCommand.Arguments, loaded.CustomCommand.Arguments);
        Assert.Equal(config.GracePeriodSec, loaded.GracePeriodSec);
    }

    [Fact]
    public void SaveThenLoad_WithNullOptionalFields_PreservesNulls()
    {
        // Arrange
        var manager = CreateConfigManager();
        var config = new AppConfig
        {
            SingleTargetMac = null,
            CustomCommand = null,
            TrustedDevices = new List<TrustedDeviceConfig>()
        };

        // Act
        manager.Save(config);

        var manager2 = CreateConfigManager();
        var loaded = manager2.Load();

        // Assert
        Assert.Null(loaded.SingleTargetMac);
        Assert.Null(loaded.CustomCommand);
        Assert.Empty(loaded.TrustedDevices);
    }

    [Fact]
    public void SaveThenLoad_MultipleSaves_LastSaveWins()
    {
        // Arrange
        var manager = CreateConfigManager();

        // Act: save multiple times with different values
        manager.Save(new AppConfig { InRangeThreshold = -60, CommandPreset = "LockWorkstation" });
        manager.Save(new AppConfig { InRangeThreshold = -55, CommandPreset = "CustomScript" });
        manager.Save(new AppConfig { InRangeThreshold = -50, CommandPreset = "MuteVolume" });

        var manager2 = CreateConfigManager();
        var loaded = manager2.Load();

        // Assert: last save wins
        Assert.Equal(-50, loaded.InRangeThreshold);
        Assert.Equal("MuteVolume", loaded.CommandPreset);
    }

    #endregion

    #region Config file creation on startup (simulating app lifecycle)

    [Fact]
    public void Startup_FirstRun_CreatesDirectoryAndConfigFile()
    {
        // Simulates first application startup where no config exists
        Assert.False(Directory.Exists(_tempDirectory));

        var manager = CreateConfigManager();
        var config = manager.Load();

        // Verify directory and file were created
        Assert.True(Directory.Exists(_tempDirectory));
        Assert.True(File.Exists(_configFilePath));

        // Verify config is usable (has valid defaults)
        Assert.Equal(-70, config.InRangeThreshold);
        Assert.Equal(-75, config.OutOfRangeThreshold);
    }

    [Fact]
    public void Startup_SubsequentRun_LoadsExistingConfig()
    {
        // Simulates first run
        var manager1 = CreateConfigManager();
        var firstConfig = manager1.Load();

        // Modify and save
        firstConfig.InRangeThreshold = -60;
        firstConfig.CommandPreset = "MuteVolume";
        manager1.Save(firstConfig);

        // Simulates second run (new ConfigManager instance)
        var manager2 = CreateConfigManager();
        var secondConfig = manager2.Load();

        // Should load the saved values, not defaults
        Assert.Equal(-60, secondConfig.InRangeThreshold);
        Assert.Equal("MuteVolume", secondConfig.CommandPreset);
    }

    #endregion

    #region Helper: Fake Notification Service

    private class FakeNotificationService : INotificationService
    {
        public List<NotificationRecord> ErrorMessages { get; } = new();
        public List<NotificationRecord> WarningMessages { get; } = new();

        public void ShowError(string message, string title)
        {
            ErrorMessages.Add(new NotificationRecord(message, title));
        }

        public void ShowWarning(string message, string title)
        {
            WarningMessages.Add(new NotificationRecord(message, title));
        }

        public string? ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            return null;
        }

        public record NotificationRecord(string Message, string Title);
    }

    #endregion
}
