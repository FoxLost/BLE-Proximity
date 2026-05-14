namespace BLEProximity.Helpers;

/// <summary>
/// Represents the color category for a device's signal strength indicator.
/// </summary>
public enum RssiColorCategory
{
    /// <summary>No color — RSSI is null or uninitialized.</summary>
    None,

    /// <summary>Green — strong signal, RSSI > -70 dBm.</summary>
    Green,

    /// <summary>Orange — moderate signal, -80 dBm ≤ RSSI ≤ -70 dBm.</summary>
    Orange,

    /// <summary>Red — weak signal, RSSI < -80 dBm.</summary>
    Red
}
