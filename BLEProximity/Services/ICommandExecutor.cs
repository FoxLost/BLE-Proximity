using BLEProximity.Models;

namespace BLEProximity.Services;

public class DeviceContext
{
    public string MacAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double SmoothedRssi { get; set; }
    public DateTime Timestamp { get; set; }
}

public interface ICommandExecutor
{
    Task ExecuteAsync(CommandConfig config, DeviceContext context);
}
