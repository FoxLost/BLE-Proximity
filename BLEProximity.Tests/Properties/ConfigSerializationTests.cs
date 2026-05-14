using System.Text.Json;
using BLEProximity.Models;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property 15: Configuration Serialization Round-Trip
/// Validates: Requirements 11.2
///
/// For any valid AppConfig instance, serializing to JSON and then deserializing SHALL produce
/// an AppConfig instance equal to the original, with all fields preserved.
/// </summary>
public class ConfigSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Generator for non-null strings suitable for config fields.
    /// Produces alphanumeric strings to avoid JSON encoding edge cases.
    /// </summary>
    private static Gen<string> NonNullStringGen =>
        Gen.Elements(
            "DeviceA", "DeviceB", "MyPhone", "Laptop", "Watch",
            "AABBCCDDEEFF", "112233445566", "FFEEDDCCBBAA",
            "LockWorkstation", "MuteVolume", "CustomScript",
            "rundll32.exe", "shutdown.exe", "powershell.exe",
            "user32.dll,LockWorkStation", "/l", "-Command test",
            "C:\\Program Files\\app.exe", "custom.bat", ""
        );

    /// <summary>
    /// Generator for MAC address strings (12-digit uppercase hex).
    /// </summary>
    private static Gen<string> MacAddressGen =>
        from chars in Gen.ListOf(12, Gen.Elements(
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F'))
        select new string(chars.ToArray());

    /// <summary>
    /// Generator for TrustedDeviceConfig instances.
    /// </summary>
    private static Gen<TrustedDeviceConfig> TrustedDeviceConfigGen =>
        from name in NonNullStringGen
        from mac in MacAddressGen
        select new TrustedDeviceConfig { Name = name, MacAddress = mac };

    /// <summary>
    /// Generator for CommandConfig instances.
    /// </summary>
    private static Gen<CommandConfig> CommandConfigGen =>
        from exe in NonNullStringGen
        from args in NonNullStringGen
        select new CommandConfig { ExecutablePath = exe, Arguments = args };

    /// <summary>
    /// Generator for nullable string fields.
    /// </summary>
    private static Gen<string?> NullableStringGen =>
        Gen.OneOf(
            Gen.Constant<string?>(null),
            MacAddressGen.Select(s => (string?)s)
        );

    /// <summary>
    /// Generator for nullable CommandConfig fields.
    /// </summary>
    private static Gen<CommandConfig?> NullableCommandConfigGen =>
        Gen.OneOf(
            Gen.Constant<CommandConfig?>(null),
            CommandConfigGen.Select(c => (CommandConfig?)c)
        );

    /// <summary>
    /// Generator for valid AppConfig instances with all fields populated.
    /// </summary>
    private static Gen<AppConfig> AppConfigGen =>
        from startWithWindows in Arb.Generate<bool>()
        from useMultiDevice in Arb.Generate<bool>()
        from darkMode in Arb.Generate<bool>()
        from singleTargetMac in NullableStringGen
        from deviceCount in Gen.Choose(0, 5)
        from trustedDevices in Gen.ListOf(deviceCount, TrustedDeviceConfigGen)
        from inRangeThreshold in Gen.Choose(-90, -50)
        from outOfRangeThreshold in Gen.Choose(-95, -55)
        from outOfRangeTimeoutSec in Gen.Choose(5, 60)
        from rssiAlpha in Gen.Elements(0.1, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5)
        from commandPreset in Gen.Elements("LockWorkstation", "MuteVolume", "CustomScript")
        from customCommand in NullableCommandConfigGen
        from gracePeriodSec in Gen.Choose(0, 30)
        from missingBeaconGraceSec in Gen.Choose(1, 30)
        select new AppConfig
        {
            StartWithWindows = startWithWindows,
            UseMultiDevice = useMultiDevice,
            DarkMode = darkMode,
            SingleTargetMac = singleTargetMac,
            TrustedDevices = trustedDevices.ToList(),
            InRangeThreshold = inRangeThreshold,
            OutOfRangeThreshold = outOfRangeThreshold,
            OutOfRangeTimeoutSec = outOfRangeTimeoutSec,
            RssiAlpha = rssiAlpha,
            CommandPreset = commandPreset,
            CustomCommand = customCommand,
            GracePeriodSec = gracePeriodSec,
            MissingBeaconGraceSec = missingBeaconGraceSec
        };

    private static Arbitrary<AppConfig> AppConfigArbitrary =>
        Arb.From(AppConfigGen);

    /// <summary>
    /// **Validates: Requirements 11.2**
    ///
    /// For any valid AppConfig instance, serializing to JSON and then deserializing
    /// SHALL produce an AppConfig instance equal to the original, with all fields preserved.
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(ConfigSerializationTests) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 15: Configuration Serialization Round-Trip")]
    public bool ConfigRoundTrip_PreservesAllFields(AppConfig original)
    {
        // Serialize to JSON
        var json = JsonSerializer.Serialize(original, JsonOptions);

        // Deserialize back
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

        if (deserialized == null)
            return false;

        // Compare all fields
        return original.StartWithWindows == deserialized.StartWithWindows
            && original.UseMultiDevice == deserialized.UseMultiDevice
            && original.DarkMode == deserialized.DarkMode
            && original.SingleTargetMac == deserialized.SingleTargetMac
            && original.InRangeThreshold == deserialized.InRangeThreshold
            && original.OutOfRangeThreshold == deserialized.OutOfRangeThreshold
            && original.OutOfRangeTimeoutSec == deserialized.OutOfRangeTimeoutSec
            && Math.Abs(original.RssiAlpha - deserialized.RssiAlpha) < 1e-10
            && original.CommandPreset == deserialized.CommandPreset
            && original.GracePeriodSec == deserialized.GracePeriodSec
            && original.MissingBeaconGraceSec == deserialized.MissingBeaconGraceSec
            && TrustedDevicesEqual(original.TrustedDevices, deserialized.TrustedDevices)
            && CommandConfigsEqual(original.CustomCommand, deserialized.CustomCommand);
    }

    /// <summary>
    /// FsCheck uses this static property to discover the Arbitrary for AppConfig.
    /// </summary>
    public static Arbitrary<AppConfig> Arbs => AppConfigArbitrary;

    private static bool TrustedDevicesEqual(
        List<TrustedDeviceConfig> original,
        List<TrustedDeviceConfig> deserialized)
    {
        if (original.Count != deserialized.Count)
            return false;

        for (int i = 0; i < original.Count; i++)
        {
            if (original[i].Name != deserialized[i].Name ||
                original[i].MacAddress != deserialized[i].MacAddress)
                return false;
        }

        return true;
    }

    private static bool CommandConfigsEqual(CommandConfig? original, CommandConfig? deserialized)
    {
        if (original == null && deserialized == null)
            return true;

        if (original == null || deserialized == null)
            return false;

        return original.ExecutablePath == deserialized.ExecutablePath
            && original.Arguments == deserialized.Arguments;
    }
}
