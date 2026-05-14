using System.IO;
using BLEProximity.Services;

namespace BLEProximity.Tests.Integration;

/// <summary>
/// Integration tests for single-instance enforcement and application lifecycle.
/// Validates: Requirements 12.1, 12.2, 12.3
/// Tests mutex behavior across processes and startup sequence ordering.
/// </summary>
public class SingleInstance_Tests : IDisposable
{
    private readonly List<SingleInstanceManager> _managers = new();
    private readonly string _testId = Guid.NewGuid().ToString("N")[..8];

    private SingleInstanceManager CreateManager()
    {
        var mutexName = $"BLEProximity_Test_{_testId}";
        var pipeName = $"BLEProximity_Pipe_Test_{_testId}";
        var manager = new SingleInstanceManager(mutexName, pipeName);
        _managers.Add(manager);
        return manager;
    }

    public void Dispose()
    {
        foreach (var manager in _managers)
        {
            manager.Dispose();
        }
    }

    #region First instance acquires mutex successfully (Requirement 12.1, 12.2)

    [Fact]
    public void FirstInstance_AcquiresMutex_Successfully()
    {
        var manager = CreateManager();

        var acquired = manager.TryAcquire();

        Assert.True(acquired, "First instance should successfully acquire the mutex");
    }

    [Fact]
    public void FirstInstance_AfterAcquire_CanStartListening()
    {
        var manager = CreateManager();
        manager.TryAcquire();

        // Should not throw - listening is valid after acquiring mutex
        var exception = Record.Exception(() => manager.StartListening(() => { }));
        Assert.Null(exception);
    }

    #endregion

    #region Second instance fails to acquire (Requirement 12.3)

    [Fact]
    public void SecondInstance_FailsToAcquire_WhenFirstHoldsMutex()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        var second = CreateManager();
        var acquired = second.TryAcquire();

        Assert.False(acquired, "Second instance should fail to acquire mutex when first holds it");
    }

    [Fact]
    public void ThirdInstance_AlsoFailsToAcquire_WhenFirstHoldsMutex()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        var second = CreateManager();
        Assert.False(second.TryAcquire());

        var third = CreateManager();
        Assert.False(third.TryAcquire(), "Third instance should also fail to acquire mutex");
    }

    #endregion

    #region After first instance disposes, second can acquire (Requirement 12.3)

    [Fact]
    public void AfterFirstDisposed_SecondInstance_CanAcquire()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        // Dispose first instance (releases mutex)
        first.Dispose();
        _managers.Remove(first);

        // Second instance should now be able to acquire
        var second = CreateManager();
        Assert.True(second.TryAcquire(), "Second instance should acquire mutex after first disposes");
    }

    [Fact]
    public void AfterFirstDisposed_SecondCanAcquire_AndThirdCannot()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());
        first.Dispose();
        _managers.Remove(first);

        var second = CreateManager();
        Assert.True(second.TryAcquire());

        var third = CreateManager();
        Assert.False(third.TryAcquire(), "Third instance should fail when second holds mutex");
    }

    #endregion

    #region Restore message IPC (send and receive) (Requirement 12.3)

    [Fact]
    public async Task RestoreMessage_SentBySecondInstance_ReceivedByFirst()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        var restoreReceived = new TaskCompletionSource<bool>();
        first.StartListening(() => restoreReceived.TrySetResult(true));

        // Allow listener to start
        await Task.Delay(150);

        // Second instance sends restore message
        var second = CreateManager();
        Assert.False(second.TryAcquire());
        second.SendRestoreMessage();

        // Wait for callback
        var completed = await Task.WhenAny(restoreReceived.Task, Task.Delay(5000));
        Assert.True(restoreReceived.Task.IsCompleted, "First instance should receive restore message from second");
        Assert.True(restoreReceived.Task.Result);
    }

    [Fact]
    public async Task RestoreMessage_MultipleMessages_AllReceived()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        int callCount = 0;
        var thirdCallReceived = new TaskCompletionSource<bool>();
        first.StartListening(() =>
        {
            var count = Interlocked.Increment(ref callCount);
            if (count >= 3)
                thirdCallReceived.TrySetResult(true);
        });

        await Task.Delay(150);

        // Send three restore messages
        for (int i = 0; i < 3; i++)
        {
            var sender = CreateManager();
            sender.SendRestoreMessage();
            await Task.Delay(200);
        }

        var completed = await Task.WhenAny(thirdCallReceived.Task, Task.Delay(10000));
        Assert.True(thirdCallReceived.Task.IsCompleted, "All three restore messages should be received");
        Assert.True(callCount >= 3);
    }

    [Fact]
    public void SendRestoreMessage_WhenNoListenerRunning_DoesNotThrow()
    {
        // Simulates sending restore when first instance isn't listening yet
        var manager = CreateManager();
        var exception = Record.Exception(() => manager.SendRestoreMessage());
        Assert.Null(exception);
    }

    #endregion

    #region Startup sequence ordering verification

    [Fact]
    public void StartupSequence_MutexAcquiredBeforeConfigLoad()
    {
        // Verifies the logical ordering: mutex must be acquired before config is loaded
        // This tests that the SingleInstanceManager can be created and used independently
        // before ConfigManager is instantiated (as done in App.xaml.cs)

        var singleInstance = CreateManager();
        bool mutexAcquired = singleInstance.TryAcquire();
        Assert.True(mutexAcquired);

        // Only after mutex is acquired do we create ConfigManager
        var tempDir = Path.Combine(Path.GetTempPath(), "BLEProximity_SeqTest_" + Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(tempDir, "config.json");
        try
        {
            var notificationService = new FakeNotificationService();
            var configManager = new ConfigManager(tempDir, configPath, notificationService);
            var config = configManager.Load();

            // Config should load successfully after mutex acquisition
            Assert.NotNull(config);
            Assert.Equal(-70, config.InRangeThreshold);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void StartupSequence_SecondInstance_TerminatesWithoutLoadingConfig()
    {
        // Verifies that when mutex acquisition fails, we don't proceed to config loading
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        var second = CreateManager();
        bool secondAcquired = second.TryAcquire();

        // Second instance should NOT proceed to config loading
        Assert.False(secondAcquired);

        // In the real app, this is where Shutdown() would be called
        // The test verifies the guard condition works correctly
    }

    [Fact]
    public void StartupSequence_AppClassExists_WithExpectedLifecycleMethods()
    {
        // Verify the App class exists and has the expected structure
        var appType = typeof(BLEProximity.App);
        Assert.NotNull(appType);

        // Verify it inherits from Application (WPF)
        Assert.True(typeof(System.Windows.Application).IsAssignableFrom(appType));

        // Verify OnStartup is overridden (it's protected, so check via reflection)
        var onStartupMethod = appType.GetMethod("OnStartup",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(onStartupMethod);
        Assert.True(onStartupMethod!.DeclaringType == appType,
            "App should override OnStartup for startup sequence");

        // Verify OnExit is overridden for shutdown sequence
        var onExitMethod = appType.GetMethod("OnExit",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(onExitMethod);
        Assert.True(onExitMethod!.DeclaringType == appType,
            "App should override OnExit for shutdown sequence");
    }

    [Fact]
    public void StartupSequence_ComponentsCanBeInitializedInOrder()
    {
        // Verifies that each component in the startup sequence can be created independently
        // Sequence: mutex → config → shortcut → services → scanner → monitor → tray

        // Step 1: Mutex (SingleInstanceManager)
        var singleInstance = CreateManager();
        Assert.True(singleInstance.TryAcquire());

        // Step 2: Config (ConfigManager)
        var tempDir = Path.Combine(Path.GetTempPath(), "BLEProximity_OrderTest_" + Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(tempDir, "config.json");
        try
        {
            var notificationService = new FakeNotificationService();
            var configManager = new ConfigManager(tempDir, configPath, notificationService);
            var config = configManager.Load();
            Assert.NotNull(config);

            // Step 3: ShortcutInstaller can be created (we don't call EnsureShortcutExists in tests)
            var shortcutInstaller = new ShortcutInstaller();
            Assert.NotNull(shortcutInstaller);

            // Step 4: RssiSmoother can be created and configured from config
            var rssiSmoother = new RssiSmoother();
            rssiSmoother.Alpha = config.RssiAlpha;
            Assert.Equal(0.3, rssiSmoother.Alpha);

            // Step 5: ProximityMonitor can be created and configured
            var proximityMonitor = new ProximityMonitor();
            proximityMonitor.Configure(new Models.ProximityConfig
            {
                InRangeThreshold = config.InRangeThreshold,
                OutOfRangeThreshold = config.OutOfRangeThreshold,
                OutOfRangeTimeoutSec = config.OutOfRangeTimeoutSec,
                GracePeriodSec = config.GracePeriodSec
            });
            Assert.Equal(Models.ProximityState.InRange, proximityMonitor.CurrentState);

            // Step 6: MultiDevicePolicy can be created from config
            var multiDevicePolicy = new MultiDevicePolicy(
                config.InRangeThreshold,
                config.OutOfRangeThreshold,
                config.OutOfRangeTimeoutSec,
                !config.UseMultiDevice);
            Assert.NotNull(multiDevicePolicy);

            // All components initialized successfully in order
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    #endregion

    #region Helper: Fake Notification Service

    private class FakeNotificationService : INotificationService
    {
        public void ShowError(string message, string title) { }
        public void ShowWarning(string message, string title) { }
    }

    #endregion
}
