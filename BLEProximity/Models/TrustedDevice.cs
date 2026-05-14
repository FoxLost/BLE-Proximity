using CommunityToolkit.Mvvm.ComponentModel;

namespace BLEProximity.Models;

public partial class TrustedDevice : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    public ulong BluetoothAddress { get; set; }
    public string MacAddress { get; set; } = string.Empty;
}
