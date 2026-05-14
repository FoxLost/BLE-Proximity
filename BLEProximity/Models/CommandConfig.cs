namespace BLEProximity.Models;

public class CommandConfig
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    public CommandConfig()
    {
    }

    public CommandConfig(string executablePath, string arguments)
    {
        ExecutablePath = executablePath;
        Arguments = arguments;
    }
}
