namespace BLEProximity.Helpers;

/// <summary>
/// Converts a 48-bit Bluetooth address (ulong) to a formatted string representation.
/// </summary>
public static class BluetoothAddressFormatter
{
    /// <summary>
    /// Formats a Bluetooth address as a 12-digit uppercase hexadecimal string with no separators.
    /// </summary>
    /// <param name="bluetoothAddress">The 48-bit Bluetooth address as a ulong.</param>
    /// <returns>A 12-character uppercase hex string, e.g. "AABBCCDDEEFF".</returns>
    public static string Format(ulong bluetoothAddress)
    {
        return (bluetoothAddress & 0xFFFFFFFFFFFF).ToString("X12");
    }
}
