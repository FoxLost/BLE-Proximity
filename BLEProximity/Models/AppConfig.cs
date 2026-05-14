namespace BLEProximity.Models;

public class AppConfig
{
    public bool StartWithWindows { get; set; } = false;
    public bool UseMultiDevice { get; set; } = false;
    public bool DarkMode { get; set; } = false;
    public string? SingleTargetMac { get; set; }
    public List<TrustedDeviceConfig> TrustedDevices { get; set; } = new();
    public int InRangeThreshold { get; set; } = -70;
    public int OutOfRangeThreshold { get; set; } = -75;
    public int OutOfRangeTimeoutSec { get; set; } = 10;
    public double RssiAlpha { get; set; } = 0.3;
    public string CommandPreset { get; set; } = "LockWorkstation";
    public CommandConfig? CustomCommand { get; set; }
    public int GracePeriodSec { get; set; } = 5;
    public int MissingBeaconGraceSec { get; set; } = 3;
}
