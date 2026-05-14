namespace BLEProximity.Models;

public class TrustedDeviceStatus
{
    public TrustedDevice Device { get; set; } = null!;
    public double? SmoothedRssi { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool IsOutOfRange { get; set; }
    public DateTime? OutOfRangeSince { get; set; }
}
