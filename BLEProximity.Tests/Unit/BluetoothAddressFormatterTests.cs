using BLEProximity.Helpers;

namespace BLEProximity.Tests.Unit;

public class BluetoothAddressFormatterTests
{
    [Fact]
    public void Format_ZeroAddress_Returns12Zeros()
    {
        var result = BluetoothAddressFormatter.Format(0UL);
        Assert.Equal("000000000000", result);
    }

    [Fact]
    public void Format_KnownAddress_ReturnsUppercaseHex()
    {
        // 0xAABBCCDDEEFF
        ulong address = 0xAABBCCDDEEFF;
        var result = BluetoothAddressFormatter.Format(address);
        Assert.Equal("AABBCCDDEEFF", result);
    }

    [Fact]
    public void Format_MaxAddress_Returns12FCharacters()
    {
        ulong address = 0xFFFFFFFFFFFF;
        var result = BluetoothAddressFormatter.Format(address);
        Assert.Equal("FFFFFFFFFFFF", result);
    }

    [Fact]
    public void Format_ResultIsAlways12Characters()
    {
        ulong address = 0x0000000001;
        var result = BluetoothAddressFormatter.Format(address);
        Assert.Equal(12, result.Length);
        Assert.Equal("000000000001", result);
    }

    [Fact]
    public void Format_MasksTo48Bits_IgnoresHigherBits()
    {
        // Set bits above 48-bit range
        ulong address = 0xFFFF_AABBCCDDEEFF;
        var result = BluetoothAddressFormatter.Format(address);
        Assert.Equal("AABBCCDDEEFF", result);
    }

    [Fact]
    public void Format_ResultContainsNoSeparators()
    {
        ulong address = 0xAABBCCDDEEFF;
        var result = BluetoothAddressFormatter.Format(address);
        Assert.DoesNotContain(":", result);
        Assert.DoesNotContain("-", result);
        Assert.DoesNotContain(" ", result);
    }

    [Fact]
    public void Format_ResultIsUppercase()
    {
        ulong address = 0xabcdef123456;
        var result = BluetoothAddressFormatter.Format(address);
        Assert.Equal(result.ToUpperInvariant(), result);
    }
}
