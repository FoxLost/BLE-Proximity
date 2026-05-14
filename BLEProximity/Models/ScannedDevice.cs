using CommunityToolkit.Mvvm.ComponentModel;

namespace BLEProximity.Models;

public partial class ScannedDevice : ObservableObject
{
    [ObservableProperty]
    private string _name = "Unknown";

    [ObservableProperty]
    private ulong _bluetoothAddress;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    [ObservableProperty]
    private double _rawRssi;

    [ObservableProperty]
    private double _smoothedRssi;

    [ObservableProperty]
    private DateTime _lastSeen;
}
