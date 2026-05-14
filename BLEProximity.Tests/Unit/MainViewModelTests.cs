using System.Collections.ObjectModel;
using BLEProximity.Models;
using BLEProximity.Services;
using BLEProximity.ViewModels;
using Moq;

namespace BLEProximity.Tests.Unit;

public class MainViewModelTests
{
    [Fact]
    public async Task ExecutingState_ExecutesCommand_WhenTrayUpdateThrows()
    {
        var commandExecuted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var bleScanner = new Mock<IBleScanner>();
        bleScanner.SetupGet(s => s.ScannedDevices).Returns(new ObservableCollection<ScannedDevice>());

        var rssiSmoother = new Mock<IRssiSmoother>();
        var proximityMonitor = new Mock<IProximityMonitor>();
        var toastNotifier = new Mock<IToastNotifier>();
        var commandExecutor = new Mock<ICommandExecutor>();
        var configManager = new Mock<IConfigManager>();
        var startupManager = new Mock<IStartupManager>();
        var trayManager = new Mock<ITrayManager>();
        var notificationService = new Mock<INotificationService>();

        configManager.Setup(m => m.Load()).Returns(new AppConfig());
        commandExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<CommandConfig>(), It.IsAny<DeviceContext>()))
            .Callback(() => commandExecuted.SetResult())
            .Returns(Task.CompletedTask);
        trayManager
            .Setup(t => t.UpdateState(It.IsAny<ProximityState>(), It.IsAny<string?>(), It.IsAny<double?>()))
            .Throws(new InvalidOperationException("Simulated tray thread failure"));

        _ = new MainViewModel(
            bleScanner.Object,
            rssiSmoother.Object,
            proximityMonitor.Object,
            toastNotifier.Object,
            commandExecutor.Object,
            configManager.Object,
            startupManager.Object,
            trayManager.Object,
            notificationService.Object);

        proximityMonitor.Raise(m => m.StateChanged += null, proximityMonitor.Object, new ProximityStateChangedEventArgs
        {
            OldState = ProximityState.Countdown,
            NewState = ProximityState.Executing
        });

        var completedTask = await Task.WhenAny(commandExecuted.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(commandExecuted.Task, completedTask);
        commandExecutor.Verify(e => e.ExecuteAsync(It.IsAny<CommandConfig>(), It.IsAny<DeviceContext>()), Times.Once);
    }

    [Fact]
    public async Task ExecutingState_DoesNotExecuteCommand_WhenExecutionIsPaused()
    {
        var bleScanner = new Mock<IBleScanner>();
        bleScanner.SetupGet(s => s.ScannedDevices).Returns(new ObservableCollection<ScannedDevice>());

        var rssiSmoother = new Mock<IRssiSmoother>();
        var proximityMonitor = new Mock<IProximityMonitor>();
        var toastNotifier = new Mock<IToastNotifier>();
        var commandExecutor = new Mock<ICommandExecutor>();
        var configManager = new Mock<IConfigManager>();
        var startupManager = new Mock<IStartupManager>();
        var trayManager = new Mock<ITrayManager>();
        var notificationService = new Mock<INotificationService>();

        configManager.Setup(m => m.Load()).Returns(new AppConfig());

        var viewModel = new MainViewModel(
            bleScanner.Object,
            rssiSmoother.Object,
            proximityMonitor.Object,
            toastNotifier.Object,
            commandExecutor.Object,
            configManager.Object,
            startupManager.Object,
            trayManager.Object,
            notificationService.Object);

        viewModel.ToggleExecutionPauseCommand.Execute(null);

        proximityMonitor.Raise(m => m.StateChanged += null, proximityMonitor.Object, new ProximityStateChangedEventArgs
        {
            OldState = ProximityState.Countdown,
            NewState = ProximityState.Executing
        });

        await Task.Delay(250);

        commandExecutor.Verify(e => e.ExecuteAsync(It.IsAny<CommandConfig>(), It.IsAny<DeviceContext>()), Times.Never);
        trayManager.Verify(t => t.SetExecutionPaused(true), Times.Once);
    }
}
