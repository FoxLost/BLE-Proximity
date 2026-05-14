using BLEProximity.Helpers;

namespace BLEProximity.Tests.Unit;

public class RssiColorClassifierTests
{
    [Fact]
    public void Classify_Null_ReturnsNone()
    {
        var result = RssiColorClassifier.Classify(null);
        Assert.Equal(RssiColorCategory.None, result);
    }

    [Theory]
    [InlineData(-69.0)]
    [InlineData(-50.0)]
    [InlineData(-10.0)]
    [InlineData(-69.9)]
    public void Classify_AboveMinus70_ReturnsGreen(double rssi)
    {
        var result = RssiColorClassifier.Classify(rssi);
        Assert.Equal(RssiColorCategory.Green, result);
    }

    [Theory]
    [InlineData(-70.0)]
    [InlineData(-75.0)]
    [InlineData(-80.0)]
    public void Classify_BetweenMinus80AndMinus70Inclusive_ReturnsOrange(double rssi)
    {
        var result = RssiColorClassifier.Classify(rssi);
        Assert.Equal(RssiColorCategory.Orange, result);
    }

    [Theory]
    [InlineData(-80.1)]
    [InlineData(-90.0)]
    [InlineData(-100.0)]
    public void Classify_BelowMinus80_ReturnsRed(double rssi)
    {
        var result = RssiColorClassifier.Classify(rssi);
        Assert.Equal(RssiColorCategory.Red, result);
    }

    [Fact]
    public void Classify_ExactBoundaryMinus70_ReturnsOrange()
    {
        // -70 is inclusive in the orange range (>= -80 and <= -70)
        var result = RssiColorClassifier.Classify(-70.0);
        Assert.Equal(RssiColorCategory.Orange, result);
    }

    [Fact]
    public void Classify_ExactBoundaryMinus80_ReturnsOrange()
    {
        // -80 is inclusive in the orange range (>= -80 and <= -70)
        var result = RssiColorClassifier.Classify(-80.0);
        Assert.Equal(RssiColorCategory.Orange, result);
    }

    [Fact]
    public void Classify_JustAboveMinus70_ReturnsGreen()
    {
        // -69.999... is strictly above -70, so green
        var result = RssiColorClassifier.Classify(-69.999);
        Assert.Equal(RssiColorCategory.Green, result);
    }

    [Fact]
    public void Classify_JustBelowMinus80_ReturnsRed()
    {
        // -80.001 is strictly below -80, so red
        var result = RssiColorClassifier.Classify(-80.001);
        Assert.Equal(RssiColorCategory.Red, result);
    }
}
