namespace BLEProximity.Services;

public interface IToastNotifier
{
    void ShowCountdownToast(string deviceName, string command, int countdownSeconds);
    void DismissCountdownToast();
    bool IsToastVisible { get; }
}
