using BLEProximity.Models;

namespace BLEProximity.Helpers;

public static class CommandPresets
{
    public static readonly Dictionary<string, CommandConfig> Presets = new()
    {
        ["LockWorkstation"] = new CommandConfig("rundll32.exe", "user32.dll,LockWorkStation"),
        ["MuteVolume"] = new CommandConfig("powershell.exe",
            "-Command \"(New-Object -ComObject WScript.Shell).SendKeys([char]173)\""),
        ["CustomScript"] = new CommandConfig("", ""),
    };
}
