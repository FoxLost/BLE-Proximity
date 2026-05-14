using BLEProximity.Models;

namespace BLEProximity.Services;

public class ConfigChangedEventArgs : EventArgs
{
    public AppConfig Config { get; set; } = null!;
}

public interface IConfigManager
{
    AppConfig Load();
    void Save(AppConfig config);
    event EventHandler<ConfigChangedEventArgs>? ConfigChanged;
}
