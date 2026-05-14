using System.Collections.ObjectModel;
using BLEProximity.Models;

namespace BLEProximity.Services;

public class BleAdvertisementReceivedEventArgs : EventArgs
{
    public string Name { get; set; } = "Unknown";
    public ulong BluetoothAddress { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public double RawRssi { get; set; }
    public double SmoothedRssi { get; set; }
    public DateTime Timestamp { get; set; }
}

public interface IBleScanner : IDisposable
{
    ObservableCollection<ScannedDevice> ScannedDevices { get; }
    bool IsScanning { get; }
    bool IsDiscoveryActive { get; }
    event EventHandler<BleAdvertisementReceivedEventArgs>? AdvertisementReceived;
    void Start();
    void Stop();
    void StartDiscovery(TimeSpan duration);
    void StopDiscovery();
    void SetTrustedDevices(IEnumerable<TrustedDevice> trustedDevices);
}
