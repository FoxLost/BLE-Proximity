namespace BLEProximity.Services;

public interface IRssiSmoother
{
    double Smooth(ulong bluetoothAddress, double rawRssi);
    void Reset(ulong bluetoothAddress);
    void ResetAll();
    double Alpha { get; set; }
}
