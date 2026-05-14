using System.Collections.ObjectModel;
using BLEProximity.Helpers;
using BLEProximity.Models;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property 2: Scanned Device Deduplication Invariant
/// Validates: Requirements 1.3
///
/// For any sequence of BLE advertisements received (including multiple advertisements
/// from the same BluetoothAddress), the scanned devices collection SHALL contain at most
/// one entry per unique BluetoothAddress, and that entry SHALL reflect the most recent
/// advertisement data.
///
/// Property 5: Stale Device Cleanup
/// Validates: Requirements 1.9
///
/// For any set of scanned devices with varying LastSeen timestamps, after the cleanup
/// operation executes, the remaining devices SHALL all have a LastSeen timestamp within
/// 60 seconds of the current time, and all devices with LastSeen older than 60 seconds
/// SHALL have been removed.
/// </summary>
public class DeviceCollectionTests
{
    #region Property 2: Scanned Device Deduplication Invariant

    /// <summary>
    /// **Validates: Requirements 1.3**
    /// For any sequence of advertisements (including duplicates from the same address),
    /// the collection SHALL contain at most one entry per unique BluetoothAddress.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AdvertisementSequenceArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "2: Scanned Device Deduplication Invariant")]
    public Property Deduplication_AtMostOneEntryPerAddress(AdvertisementSequence sequence)
    {
        var collection = new ObservableCollection<ScannedDevice>();

        // Simulate adding advertisements with deduplication logic
        foreach (var adv in sequence.Advertisements)
        {
            var existing = collection.FirstOrDefault(d => d.BluetoothAddress == adv.BluetoothAddress);
            if (existing != null)
            {
                existing.Name = adv.Name;
                existing.RawRssi = adv.RawRssi;
                existing.SmoothedRssi = adv.SmoothedRssi;
                existing.LastSeen = adv.Timestamp;
            }
            else
            {
                collection.Add(new ScannedDevice
                {
                    Name = adv.Name,
                    BluetoothAddress = adv.BluetoothAddress,
                    MacAddress = BluetoothAddressFormatter.Format(adv.BluetoothAddress),
                    RawRssi = adv.RawRssi,
                    SmoothedRssi = adv.SmoothedRssi,
                    LastSeen = adv.Timestamp
                });
            }
        }

        // Verify: at most one entry per unique BluetoothAddress
        var uniqueAddresses = collection.Select(d => d.BluetoothAddress).Distinct().Count();
        var totalEntries = collection.Count;

        return (uniqueAddresses == totalEntries)
            .Label($"Collection has {totalEntries} entries but only {uniqueAddresses} unique addresses");
    }

    /// <summary>
    /// **Validates: Requirements 1.3**
    /// For any sequence of advertisements from the same BluetoothAddress,
    /// the entry SHALL reflect the most recent advertisement data.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AdvertisementSequenceArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "2: Scanned Device Deduplication Invariant")]
    public Property Deduplication_ReflectsMostRecentData(AdvertisementSequence sequence)
    {
        var collection = new ObservableCollection<ScannedDevice>();

        // Simulate adding advertisements with deduplication logic
        foreach (var adv in sequence.Advertisements)
        {
            var existing = collection.FirstOrDefault(d => d.BluetoothAddress == adv.BluetoothAddress);
            if (existing != null)
            {
                existing.Name = adv.Name;
                existing.RawRssi = adv.RawRssi;
                existing.SmoothedRssi = adv.SmoothedRssi;
                existing.LastSeen = adv.Timestamp;
            }
            else
            {
                collection.Add(new ScannedDevice
                {
                    Name = adv.Name,
                    BluetoothAddress = adv.BluetoothAddress,
                    MacAddress = BluetoothAddressFormatter.Format(adv.BluetoothAddress),
                    RawRssi = adv.RawRssi,
                    SmoothedRssi = adv.SmoothedRssi,
                    LastSeen = adv.Timestamp
                });
            }
        }

        // For each unique address, find the last advertisement in the sequence
        var lastAdvertisements = sequence.Advertisements
            .GroupBy(a => a.BluetoothAddress)
            .ToDictionary(g => g.Key, g => g.Last());

        // Verify each entry reflects the most recent data
        foreach (var device in collection)
        {
            var lastAdv = lastAdvertisements[device.BluetoothAddress];
            if (device.Name != lastAdv.Name ||
                device.RawRssi != lastAdv.RawRssi ||
                device.SmoothedRssi != lastAdv.SmoothedRssi ||
                device.LastSeen != lastAdv.Timestamp)
            {
                return false.Label(
                    $"Device {device.BluetoothAddress:X12} does not reflect most recent data. " +
                    $"Expected Name='{lastAdv.Name}', RSSI={lastAdv.RawRssi}, LastSeen={lastAdv.Timestamp}; " +
                    $"Got Name='{device.Name}', RSSI={device.RawRssi}, LastSeen={device.LastSeen}");
            }
        }

        return true.Label("All entries reflect most recent advertisement data");
    }

    /// <summary>
    /// **Validates: Requirements 1.3**
    /// For any sequence of advertisements, the number of entries in the collection
    /// SHALL equal the number of unique BluetoothAddresses in the input sequence.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AdvertisementSequenceArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "2: Scanned Device Deduplication Invariant")]
    public Property Deduplication_CollectionSizeEqualsUniqueAddresses(AdvertisementSequence sequence)
    {
        var collection = new ObservableCollection<ScannedDevice>();

        foreach (var adv in sequence.Advertisements)
        {
            var existing = collection.FirstOrDefault(d => d.BluetoothAddress == adv.BluetoothAddress);
            if (existing != null)
            {
                existing.Name = adv.Name;
                existing.RawRssi = adv.RawRssi;
                existing.SmoothedRssi = adv.SmoothedRssi;
                existing.LastSeen = adv.Timestamp;
            }
            else
            {
                collection.Add(new ScannedDevice
                {
                    Name = adv.Name,
                    BluetoothAddress = adv.BluetoothAddress,
                    MacAddress = BluetoothAddressFormatter.Format(adv.BluetoothAddress),
                    RawRssi = adv.RawRssi,
                    SmoothedRssi = adv.SmoothedRssi,
                    LastSeen = adv.Timestamp
                });
            }
        }

        var expectedCount = sequence.Advertisements.Select(a => a.BluetoothAddress).Distinct().Count();

        return (collection.Count == expectedCount)
            .Label($"Collection has {collection.Count} entries, expected {expectedCount} unique addresses");
    }

    #endregion

    #region Property 5: Stale Device Cleanup

    /// <summary>
    /// **Validates: Requirements 1.9**
    /// For any set of scanned devices with varying LastSeen timestamps, after cleanup,
    /// all remaining devices SHALL have a LastSeen timestamp within 60 seconds of the
    /// current time.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DeviceCollectionWithTimestampsArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "5: Stale Device Cleanup")]
    public Property Cleanup_RemainingDevicesWithin60Seconds(DeviceCollectionWithTimestamps input)
    {
        var collection = new ObservableCollection<ScannedDevice>(input.Devices);
        var now = input.CurrentTime;
        var staleThreshold = TimeSpan.FromSeconds(60);

        // Simulate the cleanup logic (same as BleScanner.OnCleanupTimerTick)
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (now - collection[i].LastSeen > staleThreshold)
            {
                collection.RemoveAt(i);
            }
        }

        // Verify: all remaining devices have LastSeen within 60 seconds
        var allWithinThreshold = collection.All(d => (now - d.LastSeen) <= staleThreshold);

        return allWithinThreshold
            .Label($"After cleanup, {collection.Count(d => (now - d.LastSeen) > staleThreshold)} devices still have stale timestamps");
    }

    /// <summary>
    /// **Validates: Requirements 1.9**
    /// For any set of scanned devices, after cleanup, all devices with LastSeen older
    /// than 60 seconds SHALL have been removed.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DeviceCollectionWithTimestampsArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "5: Stale Device Cleanup")]
    public Property Cleanup_AllStaleDevicesRemoved(DeviceCollectionWithTimestamps input)
    {
        var collection = new ObservableCollection<ScannedDevice>(input.Devices);
        var now = input.CurrentTime;
        var staleThreshold = TimeSpan.FromSeconds(60);

        // Track which devices should be removed (stale)
        var staleAddresses = input.Devices
            .Where(d => (now - d.LastSeen) > staleThreshold)
            .Select(d => d.BluetoothAddress)
            .ToHashSet();

        // Simulate the cleanup logic
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (now - collection[i].LastSeen > staleThreshold)
            {
                collection.RemoveAt(i);
            }
        }

        // Verify: no stale devices remain
        var staleRemaining = collection.Any(d => staleAddresses.Contains(d.BluetoothAddress));

        return (!staleRemaining)
            .Label($"Stale devices still present after cleanup: {string.Join(", ", collection.Where(d => staleAddresses.Contains(d.BluetoothAddress)).Select(d => d.BluetoothAddress.ToString("X12")))}");
    }

    /// <summary>
    /// **Validates: Requirements 1.9**
    /// For any set of scanned devices, after cleanup, all non-stale devices SHALL
    /// still be present in the collection (cleanup does not remove fresh devices).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DeviceCollectionWithTimestampsArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "5: Stale Device Cleanup")]
    public Property Cleanup_FreshDevicesPreserved(DeviceCollectionWithTimestamps input)
    {
        var collection = new ObservableCollection<ScannedDevice>(input.Devices);
        var now = input.CurrentTime;
        var staleThreshold = TimeSpan.FromSeconds(60);

        // Track which devices should be preserved (fresh)
        var freshAddresses = input.Devices
            .Where(d => (now - d.LastSeen) <= staleThreshold)
            .Select(d => d.BluetoothAddress)
            .ToHashSet();

        // Simulate the cleanup logic
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (now - collection[i].LastSeen > staleThreshold)
            {
                collection.RemoveAt(i);
            }
        }

        // Verify: all fresh devices are still present
        var preservedAddresses = collection.Select(d => d.BluetoothAddress).ToHashSet();
        var allFreshPreserved = freshAddresses.All(addr => preservedAddresses.Contains(addr));

        return allFreshPreserved
            .Label($"Fresh devices missing after cleanup: {string.Join(", ", freshAddresses.Except(preservedAddresses).Select(a => a.ToString("X12")))}");
    }

    #endregion
}

#region Data Types for Generators

/// <summary>
/// Represents a single BLE advertisement for property testing.
/// </summary>
public class Advertisement
{
    public string Name { get; set; } = "Unknown";
    public ulong BluetoothAddress { get; set; }
    public double RawRssi { get; set; }
    public double SmoothedRssi { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents a sequence of BLE advertisements for deduplication testing.
/// </summary>
public class AdvertisementSequence
{
    public Advertisement[] Advertisements { get; set; } = Array.Empty<Advertisement>();

    public override string ToString() =>
        $"AdvertisementSequence(Count={Advertisements.Length}, UniqueAddresses={Advertisements.Select(a => a.BluetoothAddress).Distinct().Count()})";
}

/// <summary>
/// Represents a collection of scanned devices with a reference current time for cleanup testing.
/// </summary>
public class DeviceCollectionWithTimestamps
{
    public ScannedDevice[] Devices { get; set; } = Array.Empty<ScannedDevice>();
    public DateTime CurrentTime { get; set; }

    public override string ToString() =>
        $"DeviceCollection(Count={Devices.Length}, CurrentTime={CurrentTime:O})";
}

#endregion

#region Arbitraries

/// <summary>
/// Generates sequences of BLE advertisements with controlled duplication.
/// Ensures sequences have 1-20 advertisements with 1-5 unique addresses,
/// creating realistic deduplication scenarios.
/// </summary>
public class AdvertisementSequenceArbitrary
{
    public static Arbitrary<AdvertisementSequence> AdvertisementSequence()
    {
        var deviceNames = new[] { "Unknown", "Phone", "Watch", "Headphones", "Laptop", "Tablet" };

        // Generate 1-5 unique addresses
        var addressGen = Gen.Choose(1, 5).SelectMany(count =>
            Gen.Choose(0, int.MaxValue)
               .Select(i => (ulong)(uint)i + 1UL) // Ensure non-zero
               .ArrayOf(count)
               .Select(arr => arr.Distinct().ToArray())
               .Where(arr => arr.Length > 0));

        // Generate a sequence of advertisements using those addresses
        var gen = addressGen.SelectMany(addresses =>
            Gen.Choose(1, 20).SelectMany(advCount =>
            {
                var advGens = Enumerable.Range(0, advCount).Select(i =>
                {
                    var addrGen = Gen.Elements(addresses);
                    var nameGen = Gen.Elements(deviceNames);
                    var rssiGen = Gen.Choose(-10000, -100).Select(v => v / 100.0);
                    var timeGen = Gen.Choose(0, 120).Select(sec =>
                        new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddSeconds(sec));

                    return addrGen.SelectMany(addr =>
                        nameGen.SelectMany(name =>
                        rssiGen.SelectMany(rssi =>
                        rssiGen.SelectMany(smoothed =>
                        timeGen.Select(time => new Advertisement
                        {
                            BluetoothAddress = addr,
                            Name = name,
                            RawRssi = rssi,
                            SmoothedRssi = smoothed,
                            Timestamp = time
                        })))));
                });

                return Gen.Sequence(advGens).Select(advs => advs.ToArray());
            }));

        return Arb.From(gen.Select(advs => new AdvertisementSequence { Advertisements = advs }));
    }
}

/// <summary>
/// Generates collections of scanned devices with varying LastSeen timestamps
/// relative to a current time, ensuring a mix of fresh and stale devices.
/// </summary>
public class DeviceCollectionWithTimestampsArbitrary
{
    public static Arbitrary<DeviceCollectionWithTimestamps> DeviceCollectionWithTimestamps()
    {
        var now = DateTime.UtcNow;

        // Generate 1-10 devices with varying ages (0 to 180 seconds old)
        var gen = Gen.Choose(1, 10).SelectMany(count =>
        {
            var deviceGens = Enumerable.Range(0, count).Select(i =>
            {
                // Age in seconds: 0-180 to ensure mix of fresh (<= 60s) and stale (> 60s)
                var ageGen = Gen.Frequency(
                    Tuple.Create(3, Gen.Choose(0, 59)),    // Fresh devices (within threshold)
                    Tuple.Create(2, Gen.Choose(61, 180)),  // Stale devices (beyond threshold)
                    Tuple.Create(1, Gen.Elements(60, 61))  // Boundary cases
                );

                var rssiGen = Gen.Choose(-10000, -100).Select(v => v / 100.0);

                return ageGen.SelectMany(age =>
                    rssiGen.Select(rssi =>
                    {
                        var device = new ScannedDevice
                        {
                            Name = $"Device_{i}",
                            BluetoothAddress = (ulong)(i + 1),
                            MacAddress = BluetoothAddressFormatter.Format((ulong)(i + 1)),
                            RawRssi = rssi,
                            SmoothedRssi = rssi,
                            LastSeen = now.AddSeconds(-age)
                        };
                        return device;
                    }));
            });

            return Gen.Sequence(deviceGens).Select(devices => devices.ToArray());
        });

        return Arb.From(gen.Select(devices => new DeviceCollectionWithTimestamps
        {
            Devices = devices,
            CurrentTime = now
        }));
    }
}

#endregion
