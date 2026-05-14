using System.IO;
using System.Text.Json;
using BLEProximity.Models;
using BLEProximity.Services;

namespace BLEProximity.Tests.Unit;

/// <summary>
/// Unit tests for ConfigManager error handling.
/// Validates: Requirements 11.5, 11.6, 11.7
/// </summary>
public class ConfigManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _configFilePath;
    private readonly FakeNotificationService _notificationService;

    public ConfigManagerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "BLEProximity_Tests_" + Guid.NewGuid().ToString("N"));
        _configFilePath = Path.Combine(_tempDirectory, "config.json");
        _notificationService = new FakeNotificationService();
    }

    public void Dispose()
    {
        // Clean up temp files after tests
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

    #region Corrupt File Recovery (Requirement 11.6)

    [Fact]
    public void Load_MalformedJson_RenamesCorruptFileToBackup()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configFilePath, "{ this is not valid json !!!");

        var manager = CreateConfigManager();

        // Act
        manager.Load();

        // Assert
        var backupPath = _configFilePath + ".bak";
        Assert.True(File.Exists(backupPath), "Corrupt file should be renamed to .bak");
        Assert.Equal("{ this is not valid json !!!", File.ReadAllText(backupPath));
    }

    [Fact]
    public void Load_MalformedJson_CreatesNewDefaultConfig()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configFilePath, "not json at all");

        var manager = CreateConfigManager();

        // Act
        var config = manager.Load();

        // Assert
        Assert.True(File.Exists(_configFilePath), "A new config file should be created");
        Assert.Equal(-70, config.InRangeThreshold);
        Assert.Equal(-75, config.OutOfRangeThreshold);
        Assert.Equal(10, config.OutOfRangeTimeoutSec);
        Assert.Equal(0.3, config.RssiAlpha);
        Assert.Equal("LockWorkstation", config.CommandPreset);
        Assert.Equal(5, config.GracePeriodSec);
        Assert.Equal(3, config.MissingBeaconGraceSec);
        Assert.False(config.StartWithWindows);
        Assert.False(config.UseMultiDevice);
        Assert.False(config.DarkMode);
    }

    [Fact]
    public void Load_MalformedJson_ShowsWarningNotification()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configFilePath, "corrupt data");

        var manager = CreateConfigManager();

        // Act
        manager.Load();

        // Assert
        Assert.Single(_notificationService.WarningMessages);
        Assert.Contains("corrupted", _notificationService.WarningMessages[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reset", _notificationService.WarningMessages[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_NullDeserializationResult_RenamesAndCreatesDefault()
    {
        // Arrange: "null" is valid JSON but deserializes to null
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configFilePath, "null");

        var manager = CreateConfigManager();

        // Act
        var config = manager.Load();

        // Assert
        var backupPath = _configFilePath + ".bak";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(-70, config.InRangeThreshold);
        Assert.Single(_notificationService.WarningMessages);
    }

    [Fact]
    public void Load_MalformedJson_OverwritesExistingBackup()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        var backupPath = _configFilePath + ".bak";
        File.WriteAllText(backupPath, "old backup content");
        File.WriteAllText(_configFilePath, "new corrupt content");

        var manager = CreateConfigManager();

        // Act
        manager.Load();

        // Assert
        Assert.True(File.Exists(backupPath));
        Assert.Equal("new corrupt content", File.ReadAllText(backupPath));
    }

    #endregion

    #region Missing Directory Creation (Requirement 11.5)

    [Fact]
    public void Load_DirectoryDoesNotExist_CreatesDirectoryAndDefaultConfig()
    {
        // Arrange: directory does not exist yet
        Assert.False(Directory.Exists(_tempDirectory));

        var manager = CreateConfigManager();

        // Act
        var config = manager.Load();

        // Assert
        Assert.True(Directory.Exists(_tempDirectory), "Config directory should be created");
        Assert.True(File.Exists(_configFilePath), "Default config file should be created");
        Assert.Equal(-70, config.InRangeThreshold);
        Assert.Equal(-75, config.OutOfRangeThreshold);
    }

    [Fact]
    public void Load_FileDoesNotExist_CreatesDefaultConfigWithCorrectValues()
    {
        // Arrange: directory exists but file does not
        Directory.CreateDirectory(_tempDirectory);

        var manager = CreateConfigManager();

        // Act
        var config = manager.Load();

        // Assert
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
        Assert.Equal(3, config.MissingBeaconGraceSec);
    }

    [Fact]
    public void Load_FileDoesNotExist_NoNotificationShown()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);

        var manager = CreateConfigManager();

        // Act
        manager.Load();

        // Assert: no notification for normal first-run scenario
        Assert.Empty(_notificationService.WarningMessages);
        Assert.Empty(_notificationService.ErrorMessages);
    }

    [Fact]
    public void Save_DirectoryDoesNotExist_CreatesDirectoryAndSaves()
    {
        // Arrange: directory does not exist
        Assert.False(Directory.Exists(_tempDirectory));

        var manager = CreateConfigManager();
        var config = new AppConfig { InRangeThreshold = -60, OutOfRangeThreshold = -65 };

        // Act
        manager.Save(config);

        // Assert
        Assert.True(Directory.Exists(_tempDirectory));
        Assert.True(File.Exists(_configFilePath));
    }

    #endregion

    #region I/O Error Retention of In-Memory Config (Requirement 11.7)

    [Fact]
    public void Save_IoError_RetainsInMemoryConfig()
    {
        // Arrange: Load a valid config first
        Directory.CreateDirectory(_tempDirectory);
        var originalConfig = new AppConfig
        {
            InRangeThreshold = -60,
            OutOfRangeThreshold = -65,
            CommandPreset = "MuteVolume"
        };
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(originalConfig, jsonOptions));

        var manager = CreateConfigManager();
        manager.Load();

        // Now make the config file path point to a read-only file to cause I/O error
        File.SetAttributes(_configFilePath, FileAttributes.ReadOnly);

        var newConfig = new AppConfig
        {
            InRangeThreshold = -55,
            OutOfRangeThreshold = -60,
            CommandPreset = "CustomScript"
        };

        // Act
        manager.Save(newConfig);

        // Assert: in-memory config should still be the original loaded config
        // (Save failed, so _currentConfig was not updated to newConfig)
        Assert.Equal(-60, manager.CurrentConfig.InRangeThreshold);
        Assert.Equal("MuteVolume", manager.CurrentConfig.CommandPreset);

        // Cleanup: remove read-only attribute so temp dir can be deleted
        File.SetAttributes(_configFilePath, FileAttributes.Normal);
    }

    [Fact]
    public void Save_IoError_ShowsErrorNotification()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configFilePath, "{}");
        File.SetAttributes(_configFilePath, FileAttributes.ReadOnly);

        var manager = CreateConfigManager();
        var config = new AppConfig { InRangeThreshold = -55 };

        // Act
        manager.Save(config);

        // Assert
        Assert.Single(_notificationService.ErrorMessages);
        Assert.Contains("save", _notificationService.ErrorMessages[0].Message, StringComparison.OrdinalIgnoreCase);

        // Cleanup
        File.SetAttributes(_configFilePath, FileAttributes.Normal);
    }

    [Fact]
    public void Save_IoError_DoesNotFireConfigChangedEvent()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configFilePath, "{}");
        File.SetAttributes(_configFilePath, FileAttributes.ReadOnly);

        var manager = CreateConfigManager();
        bool eventFired = false;
        manager.ConfigChanged += (_, _) => eventFired = true;

        var config = new AppConfig { InRangeThreshold = -55 };

        // Act
        manager.Save(config);

        // Assert
        Assert.False(eventFired, "ConfigChanged should not fire when save fails");

        // Cleanup
        File.SetAttributes(_configFilePath, FileAttributes.Normal);
    }

    [Fact]
    public void Save_Success_FiresConfigChangedEvent()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        var manager = CreateConfigManager();
        AppConfig? receivedConfig = null;
        manager.ConfigChanged += (_, args) => receivedConfig = args.Config;

        var config = new AppConfig { InRangeThreshold = -55, CommandPreset = "MuteVolume" };

        // Act
        manager.Save(config);

        // Assert
        Assert.NotNull(receivedConfig);
        Assert.Equal(-55, receivedConfig!.InRangeThreshold);
        Assert.Equal("MuteVolume", receivedConfig.CommandPreset);
    }

    [Fact]
    public void Save_Success_UpdatesCurrentConfig()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        var manager = CreateConfigManager();
        var config = new AppConfig { InRangeThreshold = -55, CommandPreset = "MuteVolume" };

        // Act
        manager.Save(config);

        // Assert
        Assert.Equal(-55, manager.CurrentConfig.InRangeThreshold);
        Assert.Equal("MuteVolume", manager.CurrentConfig.CommandPreset);
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

        public record NotificationRecord(string Message, string Title);
    }

    #endregion
}
