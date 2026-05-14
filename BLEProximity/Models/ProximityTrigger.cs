namespace BLEProximity.Models;

public enum ProximityTrigger
{
    RssiDropped,
    RssiRecovered,
    TimeoutExpired,
    CountdownExpired,
    CommandCompleted
}
