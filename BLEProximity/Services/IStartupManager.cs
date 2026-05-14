namespace BLEProximity.Services;

public interface IStartupManager
{
    bool IsStartupEnabled { get; }
    bool SetStartupEnabled(bool enabled);
}
