namespace BLEProximity.Models;

public enum ProximityState
{
    InRange,
    OutOfRangePending,
    Countdown,
    Executing,
    Cancelled
}
