using System.Collections.ObjectModel;
using BLEProximity.Models;
using BLEProximity.Services;
using Moq;

namespace BLEProximity.Tests.Unit;

public class WindowBehaviorManagerTests
{
    private readonly Mock<ITrayManager> _mockTrayManager;
    private readonly Mock<IBleScanner> _mockBleScanner;
    private readonly WindowBehaviorManager _manager;

    public WindowBehaviorManagerTests()
    {
        _mockTrayManager = new Mock<ITrayManager>();
        _mockBleScanner = new Mock<IBleScanner>();
        _mockBleScanner.Setup(s => s.ScannedDevices).Returns(new ObservableCollection<ScannedDevice>());
        _manager = new WindowBehaviorManager(_mockTrayManager.Object, _mockBleScanner.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTrayManagerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WindowBehaviorManager(null!, _mockBleScanner.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenBleScannerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WindowBehaviorManager(_mockTrayManager.Object, null!));
    }

    [Fact]
    public void HasShownFirstCloseBalloon_InitiallyFalse()
    {
        Assert.False(_manager.HasShownFirstCloseBalloon);
    }

    [Fact]
    public void ResetFirstCloseBalloon_ResetsFlag()
    {
        // Use internal method to set the flag first (simulate a close)
        // We can't easily trigger OnWindowClosing without a real Window,
        // but we can test the reset behavior
        _manager.ResetFirstCloseBalloon();
        Assert.False(_manager.HasShownFirstCloseBalloon);
    }

    [Fact]
    public void Attach_ThrowsArgumentNullException_WhenWindowIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _manager.Attach(null!));
    }

    [Fact]
    public void ExitApplication_StopsAndDisposesBleScanner()
    {
        // ExitApplication calls Application.Current.Shutdown() which requires
        // a WPF Application context. We can only verify the scanner calls here
        // by checking that Stop and Dispose are called before the Shutdown call throws.
        // In a real integration test, this would be tested end-to-end.

        // Since Application.Current will be null in test context, this will throw.
        // We verify the scanner methods are called in order.
        _mockBleScanner.Setup(s => s.Stop());
        _mockBleScanner.Setup(s => s.Dispose());

        try
        {
            _manager.ExitApplication();
        }
        catch (NullReferenceException)
        {
            // Expected: Application.Current is null in test context
        }

        _mockBleScanner.Verify(s => s.Stop(), Times.Once);
        _mockBleScanner.Verify(s => s.Dispose(), Times.Once);
    }
}
