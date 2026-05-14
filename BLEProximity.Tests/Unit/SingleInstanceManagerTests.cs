using BLEProximity.Services;

namespace BLEProximity.Tests.Unit;

public class SingleInstanceManagerTests : IDisposable
{
    private readonly List<SingleInstanceManager> _managers = new();
    private readonly string _testId = Guid.NewGuid().ToString("N")[..8];

    private SingleInstanceManager CreateManager()
    {
        var mutexName = $"BLEProximity_UnitTest_{_testId}";
        var pipeName = $"BLEProximity_Pipe_UnitTest_{_testId}";
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

    [Fact]
    public void TryAcquire_FirstInstance_ReturnsTrue()
    {
        var manager = CreateManager();
        var result = manager.TryAcquire();
        Assert.True(result);
    }

    [Fact]
    public void TryAcquire_SecondInstance_ReturnsFalse()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        var second = CreateManager();
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void TryAcquire_AfterFirstDisposed_SecondCanAcquire()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());
        first.Dispose();

        var second = CreateManager();
        Assert.True(second.TryAcquire());
    }

    [Fact]
    public void StartListening_WithoutAcquire_ThrowsInvalidOperation()
    {
        var manager = CreateManager();
        Assert.Throws<InvalidOperationException>(() => manager.StartListening(() => { }));
    }

    [Fact]
    public async Task SendRestoreMessage_TriggersCallback()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        var restoreReceived = new TaskCompletionSource<bool>();
        first.StartListening(() => restoreReceived.TrySetResult(true));

        // Give the listener time to start
        await Task.Delay(100);

        var second = CreateManager();
        Assert.False(second.TryAcquire());
        second.SendRestoreMessage();

        var result = await Task.WhenAny(restoreReceived.Task, Task.Delay(5000));
        Assert.True(restoreReceived.Task.IsCompleted, "Restore callback was not invoked within timeout");
        Assert.True(restoreReceived.Task.Result);
    }

    [Fact]
    public void SendRestoreMessage_NoListener_DoesNotThrow()
    {
        // When no instance is listening, SendRestoreMessage should not throw
        var manager = CreateManager();
        var exception = Record.Exception(() => manager.SendRestoreMessage());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var manager = CreateManager();
        manager.TryAcquire();
        manager.Dispose();

        var exception = Record.Exception(() => manager.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public async Task StartListening_MultipleRestoreMessages_AllTriggerCallback()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        int callCount = 0;
        var secondCallReceived = new TaskCompletionSource<bool>();
        first.StartListening(() =>
        {
            var count = Interlocked.Increment(ref callCount);
            if (count >= 2)
                secondCallReceived.TrySetResult(true);
        });

        await Task.Delay(100);

        // Send two restore messages
        var sender1 = CreateManager();
        sender1.SendRestoreMessage();

        await Task.Delay(200);

        var sender2 = CreateManager();
        sender2.SendRestoreMessage();

        var result = await Task.WhenAny(secondCallReceived.Task, Task.Delay(5000));
        Assert.True(secondCallReceived.Task.IsCompleted, "Second restore callback was not invoked within timeout");
        Assert.True(callCount >= 2);
    }
}
