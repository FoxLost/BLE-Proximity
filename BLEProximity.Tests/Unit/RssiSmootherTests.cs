using BLEProximity.Services;

namespace BLEProximity.Tests.Unit;

public class RssiSmootherTests
{
    private readonly RssiSmoother _smoother = new();

    [Fact]
    public void Smooth_FirstValidReading_ReturnsRawValue()
    {
        ulong address = 0xAABBCCDDEEFF;
        double rawRssi = -65.0;

        double result = _smoother.Smooth(address, rawRssi);

        Assert.Equal(rawRssi, result);
    }

    [Fact]
    public void Smooth_SecondReading_AppliesEmaFormula()
    {
        ulong address = 0xAABBCCDDEEFF;
        _smoother.Alpha = 0.3;

        _smoother.Smooth(address, -60.0); // First reading initializes directly
        double result = _smoother.Smooth(address, -70.0); // Second applies EMA

        // Expected: 0.3 * (-70) + 0.7 * (-60) = -21 + -42 = -63
        double expected = 0.3 * (-70.0) + 0.7 * (-60.0);
        Assert.Equal(expected, result, precision: 10);
    }

    [Fact]
    public void Smooth_MultipleReadings_AppliesEmaSequentially()
    {
        ulong address = 0xAABBCCDDEEFF;
        _smoother.Alpha = 0.3;

        double r1 = _smoother.Smooth(address, -60.0); // -60
        double r2 = _smoother.Smooth(address, -70.0); // 0.3*(-70) + 0.7*(-60) = -63
        double r3 = _smoother.Smooth(address, -50.0); // 0.3*(-50) + 0.7*(-63) = -59.1

        Assert.Equal(-60.0, r1);
        Assert.Equal(-63.0, r2, precision: 10);
        Assert.Equal(0.3 * (-50.0) + 0.7 * r2, r3, precision: 10);
    }

    [Fact]
    public void Smooth_InvalidRssiZero_ThrowsWhenNoExistingState()
    {
        ulong address = 0xAABBCCDDEEFF;

        Assert.Throws<ArgumentException>(() => _smoother.Smooth(address, 0));
    }

    [Fact]
    public void Smooth_InvalidRssiPositive_ThrowsWhenNoExistingState()
    {
        ulong address = 0xAABBCCDDEEFF;

        Assert.Throws<ArgumentException>(() => _smoother.Smooth(address, 10.0));
    }

    [Fact]
    public void Smooth_InvalidRssiZero_ReturnsPreviousSmoothedValue()
    {
        ulong address = 0xAABBCCDDEEFF;

        _smoother.Smooth(address, -65.0); // Initialize
        double result = _smoother.Smooth(address, 0); // Invalid, should return previous

        Assert.Equal(-65.0, result);
    }

    [Fact]
    public void Smooth_InvalidRssiPositive_ReturnsPreviousSmoothedValue()
    {
        ulong address = 0xAABBCCDDEEFF;

        _smoother.Smooth(address, -65.0); // Initialize
        double result = _smoother.Smooth(address, 5.0); // Invalid, should return previous

        Assert.Equal(-65.0, result);
    }

    [Fact]
    public void Alpha_DefaultValue_Is03()
    {
        Assert.Equal(0.3, _smoother.Alpha);
    }

    [Fact]
    public void Alpha_SetBelowMinimum_ClampsTo01()
    {
        _smoother.Alpha = 0.01;
        Assert.Equal(0.1, _smoother.Alpha);
    }

    [Fact]
    public void Alpha_SetAboveMaximum_ClampsTo05()
    {
        _smoother.Alpha = 0.9;
        Assert.Equal(0.5, _smoother.Alpha);
    }

    [Fact]
    public void Alpha_SetWithinRange_UsesExactValue()
    {
        _smoother.Alpha = 0.25;
        Assert.Equal(0.25, _smoother.Alpha);
    }

    [Fact]
    public void Alpha_SetToMinBound_Accepted()
    {
        _smoother.Alpha = 0.1;
        Assert.Equal(0.1, _smoother.Alpha);
    }

    [Fact]
    public void Alpha_SetToMaxBound_Accepted()
    {
        _smoother.Alpha = 0.5;
        Assert.Equal(0.5, _smoother.Alpha);
    }

    [Fact]
    public void Reset_ClearsStateForSpecificDevice()
    {
        ulong address1 = 0xAABBCCDDEEFF;
        ulong address2 = 0x112233445566;

        _smoother.Smooth(address1, -60.0);
        _smoother.Smooth(address2, -70.0);

        _smoother.Reset(address1);

        // address1 should be treated as new device (first reading initializes directly)
        double result1 = _smoother.Smooth(address1, -80.0);
        Assert.Equal(-80.0, result1);

        // address2 should still have its state (EMA applied)
        double result2 = _smoother.Smooth(address2, -60.0);
        double expected2 = 0.3 * (-60.0) + 0.7 * (-70.0);
        Assert.Equal(expected2, result2, precision: 10);
    }

    [Fact]
    public void ResetAll_ClearsAllDeviceStates()
    {
        ulong address1 = 0xAABBCCDDEEFF;
        ulong address2 = 0x112233445566;

        _smoother.Smooth(address1, -60.0);
        _smoother.Smooth(address2, -70.0);

        _smoother.ResetAll();

        // Both should be treated as new devices
        double result1 = _smoother.Smooth(address1, -80.0);
        double result2 = _smoother.Smooth(address2, -50.0);

        Assert.Equal(-80.0, result1);
        Assert.Equal(-50.0, result2);
    }

    [Fact]
    public void Smooth_IndependentPerDevice()
    {
        ulong address1 = 0xAABBCCDDEEFF;
        ulong address2 = 0x112233445566;
        _smoother.Alpha = 0.3;

        // First readings for both devices
        double r1a = _smoother.Smooth(address1, -60.0);
        double r2a = _smoother.Smooth(address2, -80.0);

        Assert.Equal(-60.0, r1a);
        Assert.Equal(-80.0, r2a);

        // Second readings - EMA applied independently
        double r1b = _smoother.Smooth(address1, -70.0);
        double r2b = _smoother.Smooth(address2, -60.0);

        Assert.Equal(0.3 * (-70.0) + 0.7 * (-60.0), r1b, precision: 10);
        Assert.Equal(0.3 * (-60.0) + 0.7 * (-80.0), r2b, precision: 10);
    }

    [Fact]
    public void Reset_NonExistentDevice_DoesNotThrow()
    {
        // Should not throw when resetting a device that was never tracked
        _smoother.Reset(0xDEADBEEF);
    }

    [Fact]
    public void ResetAll_WhenEmpty_DoesNotThrow()
    {
        _smoother.ResetAll();
    }
}
