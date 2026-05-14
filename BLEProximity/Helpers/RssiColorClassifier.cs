namespace BLEProximity.Helpers;

/// <summary>
/// Classifies a smoothed RSSI value into a color category for visual indication.
/// </summary>
public static class RssiColorClassifier
{
    /// <summary>
    /// Returns the color category based on the smoothed RSSI value.
    /// </summary>
    /// <param name="smoothedRssi">The smoothed RSSI value in dBm, or null if uninitialized.</param>
    /// <returns>
    /// Green if RSSI > -70 dBm,
    /// Orange if -80 ≤ RSSI ≤ -70 dBm,
    /// Red if RSSI &lt; -80 dBm,
    /// None if null/uninitialized.
    /// </returns>
    public static RssiColorCategory Classify(double? smoothedRssi)
    {
        if (smoothedRssi is null)
            return RssiColorCategory.None;

        double rssi = smoothedRssi.Value;

        if (rssi > -70.0)
            return RssiColorCategory.Green;

        if (rssi >= -80.0) // -80 <= rssi <= -70
            return RssiColorCategory.Orange;

        return RssiColorCategory.Red; // rssi < -80
    }
}
