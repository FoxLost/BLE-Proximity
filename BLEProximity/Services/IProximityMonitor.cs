using BLEProximity.Models;

namespace BLEProximity.Services;

public class ProximityStateChangedEventArgs : EventArgs
{
    public ProximityState OldState { get; set; }
    public ProximityState NewState { get; set; }
}

public interface IProximityMonitor
{
    ProximityState CurrentState { get; }
    event EventHandler<ProximityStateChangedEventArgs>? StateChanged;
    void UpdateRssi(ulong bluetoothAddress, double smoothedRssi);
    void Start();
    void Stop();
    void Configure(ProximityConfig config);
}
