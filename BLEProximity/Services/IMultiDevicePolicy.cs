using BLEProximity.Models;

namespace BLEProximity.Services;

public interface IMultiDevicePolicy
{
    bool ShouldTriggerOutOfRange(IReadOnlyList<TrustedDeviceStatus> devices);
    bool ShouldCancelOutOfRange(IReadOnlyList<TrustedDeviceStatus> devices);
}
