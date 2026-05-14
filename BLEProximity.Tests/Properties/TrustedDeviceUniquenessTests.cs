using System.Collections.ObjectModel;
using BLEProximity.Helpers;
using BLEProximity.Models;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property 4: Trusted Device Uniqueness
/// Validates: Requirements 1.7
///
/// For any sequence of add-to-trusted operations (including repeated additions of the
/// same BluetoothAddress), the trusted device list SHALL never contain more than one
/// entry per unique BluetoothAddress.
/// </summary>
public class TrustedDeviceUniquenessTests
{
    /// <summary>
    /// Simulates the AddTrustedDevice logic from MainViewModel:
    /// checks if a device with the same BluetoothAddress already exists before adding.
    /// </summary>
    private static void AddTrustedDevice(ObservableCollection<TrustedDevice> trustedDevices, ScannedDevice device)
    {
        // Same duplicate check as MainViewModel.AddTrustedDevice
        if (trustedDevices.Any(td => td.BluetoothAddress == device.BluetoothAddress))
        {
            return;
        }

        trustedDevices.Add(new TrustedDevice
        {
            Name = device.Name,
            BluetoothAddress = device.BluetoothAddress,
            MacAddress = device.MacAddress
        });
    }

    /// <summary>
    /// **Validates: Requirements 1.7**
    /// For any sequence of add-to-trusted operations (including repeated additions of the
    /// same BluetoothAddress), the trusted device list SHALL never contain more than one
    /// entry per unique BluetoothAddress.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AddToTrustedSequenceArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "4: Trusted Device Uniqueness")]
    public Property TrustedDevices_NeverContainDuplicateBluetoothAddresses(AddToTrustedSequence sequence)
    {
        var trustedDevices = new ObservableCollection<TrustedDevice>();

        // Simulate all add-to-trusted operations
        foreach (var device in sequence.Devices)
        {
            AddTrustedDevice(trustedDevices, device);
        }

        // Verify: no duplicate BluetoothAddresses in the trusted list
        var uniqueAddresses = trustedDevices.Select(td => td.BluetoothAddress).Distinct().Count();
        var totalEntries = trustedDevices.Count;

        return (uniqueAddresses == totalEntries)
            .Label($"Trusted list has {totalEntries} entries but only {uniqueAddresses} unique addresses");
    }

    /// <summary>
    /// **Validates: Requirements 1.7**
    /// For any sequence of add-to-trusted operations, the number of entries in the
    /// trusted list SHALL equal the number of unique BluetoothAddresses in the input.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AddToTrustedSequenceArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "4: Trusted Device Uniqueness")]
    public Property TrustedDevices_CountEqualsUniqueAddresses(AddToTrustedSequence sequence)
    {
        var trustedDevices = new ObservableCollection<TrustedDevice>();

        foreach (var device in sequence.Devices)
        {
            AddTrustedDevice(trustedDevices, device);
        }

        var expectedCount = sequence.Devices.Select(d => d.BluetoothAddress).Distinct().Count();

        return (trustedDevices.Count == expectedCount)
            .Label($"Trusted list has {trustedDevices.Count} entries, expected {expectedCount} unique addresses from {sequence.Devices.Length} add operations");
    }

    /// <summary>
    /// **Validates: Requirements 1.7**
    /// For any sequence of add-to-trusted operations where the same device is added
    /// multiple times, the uniqueness invariant holds at every intermediate step.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AddToTrustedSequenceArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "4: Trusted Device Uniqueness")]
    public Property TrustedDevices_UniquenessHoldsAtEveryStep(AddToTrustedSequence sequence)
    {
        var trustedDevices = new ObservableCollection<TrustedDevice>();

        foreach (var device in sequence.Devices)
        {
            AddTrustedDevice(trustedDevices, device);

            // Check invariant after every add operation
            var uniqueCount = trustedDevices.Select(td => td.BluetoothAddress).Distinct().Count();
            if (uniqueCount != trustedDevices.Count)
            {
                return false.Label(
                    $"Uniqueness invariant violated after adding device {device.BluetoothAddress:X12}. " +
                    $"List has {trustedDevices.Count} entries but only {uniqueCount} unique addresses");
            }
        }

        return true.Label("Uniqueness invariant held at every step");
    }
}

#region Data Types for Trusted Device Uniqueness Generators

/// <summary>
/// Represents a sequence of add-to-trusted operations, including potential duplicates.
/// </summary>
public class AddToTrustedSequence
{
    public ScannedDevice[] Devices { get; set; } = Array.Empty<ScannedDevice>();

    public override string ToString()
    {
        var uniqueCount = Devices.Select(d => d.BluetoothAddress).Distinct().Count();
        return $"AddToTrustedSequence(Operations={Devices.Length}, UniqueAddresses={uniqueCount})";
    }
}

#endregion

#region Arbitraries for Trusted Device Uniqueness

/// <summary>
/// Generates sequences of ScannedDevice objects for add-to-trusted operations.
/// Ensures sequences contain 1-20 operations with 1-8 unique BluetoothAddresses,
/// creating realistic scenarios with intentional duplicates.
/// </summary>
public class AddToTrustedSequenceArbitrary
{
    public static Arbitrary<AddToTrustedSequence> AddToTrustedSequence()
    {
        var deviceNames = new[] { "Unknown", "Phone", "Watch", "Headphones", "Laptop", "Tablet", "Speaker", "Band" };

        // Generate 1-8 unique addresses (within the 10-device max limit)
        var addressGen = Gen.Choose(1, 8).SelectMany(count =>
            Gen.Choose(1, int.MaxValue)
               .Select(i => (ulong)(uint)i)
               .ArrayOf(count)
               .Select(arr => arr.Distinct().Where(a => a > 0).ToArray())
               .Where(arr => arr.Length > 0));

        // Generate a sequence of add operations using those addresses (with duplicates)
        var gen = addressGen.SelectMany(addresses =>
            Gen.Choose(1, 20).SelectMany(opCount =>
            {
                var deviceGens = Enumerable.Range(0, opCount).Select(_ =>
                {
                    var addrGen = Gen.Elements(addresses);
                    var nameGen = Gen.Elements(deviceNames);
                    var rssiGen = Gen.Choose(-10000, -100).Select(v => v / 100.0);

                    return addrGen.SelectMany(addr =>
                        nameGen.SelectMany(name =>
                        rssiGen.Select(rssi =>
                        {
                            var device = new ScannedDevice
                            {
                                Name = name,
                                BluetoothAddress = addr,
                                MacAddress = BluetoothAddressFormatter.Format(addr),
                                RawRssi = rssi,
                                SmoothedRssi = rssi,
                                LastSeen = DateTime.UtcNow
                            };
                            return device;
                        })));
                });

                return Gen.Sequence(deviceGens).Select(devices => devices.ToArray());
            }));

        return Arb.From(gen.Select(devices => new AddToTrustedSequence { Devices = devices }));
    }
}

#endregion
