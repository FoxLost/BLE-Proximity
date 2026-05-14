namespace BLEProximity.Models;

public class ProximityConfig
{
    public int InRangeThreshold { get; set; }
    public int OutOfRangeThreshold { get; set; }
    public int OutOfRangeTimeoutSec { get; set; }
    public int GracePeriodSec { get; set; }
}
