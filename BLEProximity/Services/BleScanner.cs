using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using BLEProximity.Helpers;
using BLEProximity.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BLEProximity.Services;

/// <summary>
/// Implements passive BLE advertisement scanning using BluetoothLEAdvertisementWatcher.
/// Maintains an observable collection of scanned devices with deduplication,
/// RSSI smoothing, and stale device cleanup.
/// </summary>
public class BleScanner : IBleScanner
{
    private readonly IRssiSmoother _rssiSmoother;
    private readonly INotificationService _notificationService;
    private readonly Dispatcher _dispatcher;

    private BluetoothLEAdvertisementWatcher? _watcher;
    private DispatcherTimer? _cleanupTimer;
    private DispatcherTimer? _discoveryTimer;
    private bool _disposed;
    private bool _isDiscoveryActive;
    private readonly object _trustedDevicesLock = new();
    private Dictionary<string, string> _trustedDeviceNamesByMac = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<ulong, byte> _pendingNameResolutions = new();

    private static readonly TimeSpan StaleDeviceThreshold = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(60);

    // Cache resolved device names to avoid repeated lookups
    private readonly ConcurrentDictionary<ulong, string> _resolvedNames = new();

    public ObservableCollection<ScannedDevice> ScannedDevices { get; } = new();

    public bool IsScanning { get; private set; }

    public bool IsDiscoveryActive => _isDiscoveryActive;

    public event EventHandler<BleAdvertisementReceivedEventArgs>? AdvertisementReceived;

    public BleScanner(IRssiSmoother rssiSmoother, INotificationService notificationService, Dispatcher dispatcher)
    {
        _rssiSmoother = rssiSmoother ?? throw new ArgumentNullException(nameof(rssiSmoother));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BleScanner));

        if (IsScanning)
            return;

        try
        {
            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Passive
            };

            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += OnWatcherStopped;

            _watcher.Start();
            IsScanning = true;

            // Start the stale device cleanup timer
            _cleanupTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = CleanupInterval
            };
            _cleanupTimer.Tick += OnCleanupTimerTick;
            _cleanupTimer.Start();
        }
        catch (Exception ex) when (
            ex is PlatformNotSupportedException ||
            ex is System.Runtime.InteropServices.COMException)
        {
            // BLE adapter unavailable or watcher start failure: display error, disable scanning
            IsScanning = false;
            _notificationService.ShowError(
                "Bluetooth hardware is not available. BLE scanning has been disabled.",
                "BLE Scanner Error");
        }
    }

    public void Stop()
    {
        if (!IsScanning)
            return;

        _cleanupTimer?.Stop();
        _cleanupTimer = null;
        StopDiscovery();

        if (_watcher != null)
        {
            _watcher.Received -= OnAdvertisementReceived;
            _watcher.Stopped -= OnWatcherStopped;
            _watcher.Stop();
            _watcher = null;
        }

        IsScanning = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
        ScannedDevices.Clear();
    }

    public void StartDiscovery(TimeSpan duration)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BleScanner));

        if (!IsScanning)
            Start();

        _dispatcher.InvokeAsync(RemoveUntrustedDevices);

        _isDiscoveryActive = true;

        _discoveryTimer?.Stop();
        _discoveryTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = duration
        };
        _discoveryTimer.Tick += (_, _) => StopDiscovery();
        _discoveryTimer.Start();
    }

    public void StopDiscovery()
    {
        _isDiscoveryActive = false;
        _discoveryTimer?.Stop();
        _discoveryTimer = null;
    }

    public void SetTrustedDevices(IEnumerable<TrustedDevice> trustedDevices)
    {
        var namesByMac = trustedDevices
            .Where(device => !string.IsNullOrWhiteSpace(device.MacAddress))
            .GroupBy(device => device.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => string.IsNullOrWhiteSpace(group.First().Name) ? group.Key : group.First().Name,
                StringComparer.OrdinalIgnoreCase);

        lock (_trustedDevicesLock)
        {
            _trustedDeviceNamesByMac = namesByMac;
        }
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var bluetoothAddress = args.BluetoothAddress;
        var macAddress = BluetoothAddressFormatter.Format(bluetoothAddress);
        var trustedName = GetTrustedDeviceName(macAddress);
        var isTrustedDevice = trustedName != null;
        var isDiscoveryActive = _isDiscoveryActive;

        if (!isTrustedDevice && !isDiscoveryActive)
            return;

        var rawRssi = (double)args.RawSignalStrengthInDBm;
        var timestamp = args.Timestamp.UtcDateTime;

        // Try advertisement names first. Some devices put the name in raw
        // advertising data sections instead of Advertisement.LocalName.
        string? advertisedName = NormalizeDeviceName(args.Advertisement.LocalName)
            ?? GetNameFromAdvertisementDataSections(args.Advertisement);

        // Apply RSSI smoothing
        double smoothedRssi;
        try
        {
            smoothedRssi = _rssiSmoother.Smooth(bluetoothAddress, rawRssi);
        }
        catch (ArgumentException)
        {
            // Invalid RSSI value (0 or positive), discard this advertisement
            return;
        }

        var deviceName = advertisedName
            ?? trustedName
            ?? GetKnownDeviceName(bluetoothAddress)
            ?? "Unknown";

        if (deviceName == "Unknown" && isDiscoveryActive)
        {
            FireAndUpdate(deviceName, bluetoothAddress, macAddress, rawRssi, smoothedRssi, timestamp);
            _ = ResolveDeviceNameForDiscoveryAsync(bluetoothAddress, macAddress, rawRssi, smoothedRssi, timestamp);
            return;
        }

        // Cache the resolved name if it's meaningful
        if (deviceName != "Unknown")
        {
            _resolvedNames[bluetoothAddress] = deviceName;
        }

        FireAndUpdate(deviceName, bluetoothAddress, macAddress, rawRssi, smoothedRssi, timestamp);
    }

    private async Task ResolveDeviceNameForDiscoveryAsync(
        ulong bluetoothAddress,
        string macAddress,
        double rawRssi,
        double smoothedRssi,
        DateTime timestamp)
    {
        if (!_pendingNameResolutions.TryAdd(bluetoothAddress, 0))
            return;

        try
        {
            var deviceName = await ResolveNameFromDeviceInformationAsync(bluetoothAddress)
                ?? "Unknown";

            if (deviceName == "Unknown")
            {
                try
                {
                    using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                    if (device != null)
                    {
                        deviceName = NormalizeDeviceName(device.Name)
                            ?? NormalizeDeviceName(device.DeviceInformation?.Name)
                            ?? GetNameFromDeviceInformationProperties(device.DeviceInformation)
                            ?? "Unknown";
                    }
                }
                catch
                {
                    // Name lookup is best-effort during manual discovery.
                }
            }

            if (deviceName != "Unknown")
            {
                _resolvedNames[bluetoothAddress] = deviceName;
            }

            FireAndUpdate(deviceName, bluetoothAddress, macAddress, rawRssi, smoothedRssi, timestamp);
        }
        finally
        {
            _pendingNameResolutions.TryRemove(bluetoothAddress, out _);
        }
    }

    private static string? GetNameFromAdvertisementDataSections(BluetoothLEAdvertisement advertisement)
    {
        foreach (var section in advertisement.DataSections)
        {
            if (section.DataType != 0x08 && section.DataType != 0x09)
                continue;

            var length = (int)section.Data.Length;
            if (length <= 0 || length > 248)
                continue;

            try
            {
                var bytes = new byte[length];
                using var reader = DataReader.FromBuffer(section.Data);
                reader.ReadBytes(bytes);
                var name = Encoding.UTF8.GetString(bytes);
                var normalizedName = NormalizeDeviceName(name);
                if (normalizedName != null)
                    return normalizedName;
            }
            catch
            {
                // Ignore malformed advertisement data.
            }
        }

        return null;
    }

    private static async Task<string?> ResolveNameFromDeviceInformationAsync(ulong bluetoothAddress)
    {
        try
        {
            var selector = BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(bluetoothAddress);
            var devices = await DeviceInformation.FindAllAsync(selector, DeviceNameProperties);
            foreach (var deviceInfo in devices)
            {
                var name = NormalizeDeviceName(deviceInfo.Name)
                    ?? GetNameFromDeviceInformationProperties(deviceInfo);
                if (name != null)
                    return name;
            }
        }
        catch
        {
            // The device information database is not always available for
            // transient BLE advertisements.
        }

        return null;
    }

    private static string? GetNameFromDeviceInformationProperties(DeviceInformation? deviceInformation)
    {
        if (deviceInformation == null)
            return null;

        foreach (var propertyName in DeviceNameProperties)
        {
            if (deviceInformation.Properties.TryGetValue(propertyName, out var value))
            {
                var name = NormalizeDeviceName(value as string);
                if (name != null)
                    return name;
            }
        }

        return null;
    }

    private static string? NormalizeDeviceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim('\0', ' ', '\t', '\r', '\n');
        if (trimmed.Length == 0
            || trimmed.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Bluetooth ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private static readonly string[] DeviceNameProperties =
    [
        "System.ItemNameDisplay",
        "System.Devices.FriendlyName",
        "System.Devices.Name"
    ];

    private string? GetTrustedDeviceName(string macAddress)
    {
        lock (_trustedDevicesLock)
        {
            return _trustedDeviceNamesByMac.TryGetValue(macAddress, out var name) ? name : null;
        }
    }

    private string? GetKnownDeviceName(ulong bluetoothAddress)
    {
        if (_resolvedNames.TryGetValue(bluetoothAddress, out var cachedName))
            return cachedName;

        var existing = FindDeviceByAddress(bluetoothAddress);
        if (existing != null && existing.Name != "Unknown" && !existing.Name.StartsWith("Bluetooth "))
            return existing.Name;

        return null;
    }

    private void FireAndUpdate(string deviceName, ulong bluetoothAddress, string macAddress, double rawRssi, double smoothedRssi, DateTime timestamp)
    {
        // Fire the event
        var eventArgs = new BleAdvertisementReceivedEventArgs
        {
            Name = deviceName,
            BluetoothAddress = bluetoothAddress,
            MacAddress = macAddress,
            RawRssi = rawRssi,
            SmoothedRssi = smoothedRssi,
            Timestamp = timestamp
        };
        AdvertisementReceived?.Invoke(this, eventArgs);

        // Marshal UI updates to the dispatcher thread
        _dispatcher.InvokeAsync(() =>
        {
            UpdateScannedDevice(deviceName, bluetoothAddress, macAddress, rawRssi, smoothedRssi, timestamp);
        });
    }

    private void UpdateScannedDevice(string name, ulong bluetoothAddress, string macAddress, double rawRssi, double smoothedRssi, DateTime timestamp)
    {
        // Deduplicate by BluetoothAddress
        var existingDevice = FindDeviceByAddress(bluetoothAddress);

        if (existingDevice != null)
        {
            // Only update name if the new name is not "Unknown" or the existing name is also "Unknown"
            if (name != "Unknown" || existingDevice.Name == "Unknown")
            {
                existingDevice.Name = name;
            }
            existingDevice.RawRssi = rawRssi;
            existingDevice.SmoothedRssi = smoothedRssi;
            existingDevice.LastSeen = timestamp;
        }
        else
        {
            // Add new device
            var newDevice = new ScannedDevice
            {
                Name = name,
                BluetoothAddress = bluetoothAddress,
                MacAddress = macAddress,
                RawRssi = rawRssi,
                SmoothedRssi = smoothedRssi,
                LastSeen = timestamp
            };
            ScannedDevices.Add(newDevice);
        }
    }

    private ScannedDevice? FindDeviceByAddress(ulong bluetoothAddress)
    {
        for (int i = 0; i < ScannedDevices.Count; i++)
        {
            if (ScannedDevices[i].BluetoothAddress == bluetoothAddress)
                return ScannedDevices[i];
        }
        return null;
    }

    private void OnCleanupTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        for (int i = ScannedDevices.Count - 1; i >= 0; i--)
        {
            if (now - ScannedDevices[i].LastSeen > StaleDeviceThreshold)
            {
                ScannedDevices.RemoveAt(i);
            }
        }
    }

    private void RemoveUntrustedDevices()
    {
        for (int i = ScannedDevices.Count - 1; i >= 0; i--)
        {
            if (GetTrustedDeviceName(ScannedDevices[i].MacAddress) == null)
            {
                ScannedDevices.RemoveAt(i);
            }
        }
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        // Only disable scanning for adapter-related errors
        if (args.Error == BluetoothError.RadioNotAvailable ||
            args.Error == BluetoothError.ResourceInUse)
        {
            _dispatcher.InvokeAsync(() =>
            {
                IsScanning = false;
                _cleanupTimer?.Stop();
                _notificationService.ShowError(
                    "Bluetooth hardware is not available. BLE scanning has been disabled.",
                    "BLE Scanner Error");
            });
        }
        // Other errors (permissions, resource constraints) do NOT disable scanning
    }
}
