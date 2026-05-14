namespace BLEProximity.Services;

/// <summary>
/// Applies Exponential Moving Average (EMA) smoothing to raw RSSI values
/// to reduce signal noise. Maintains independent smoothing state per device.
/// </summary>
public class RssiSmoother : IRssiSmoother
{
    private readonly Dictionary<ulong, double> _smoothedValues = new();
    private readonly Dictionary<ulong, DateTime> _lastSeenTimestamps = new();

    private double _alpha = 0.3;
    private TimeSpan _outOfRangeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the EMA alpha parameter, clamped to [0.1, 0.5].
    /// </summary>
    public double Alpha
    {
        get => _alpha;
        set => _alpha = Math.Clamp(value, 0.1, 0.5);
    }

    /// <summary>
    /// Gets or sets the out-of-range timeout duration.
    /// When a device is detected again after this timeout has expired,
    /// its smoothing state is reset.
    /// </summary>
    public TimeSpan OutOfRangeTimeout
    {
        get => _outOfRangeTimeout;
        set => _outOfRangeTimeout = value;
    }

    /// <summary>
    /// Computes the smoothed RSSI value for a device using EMA.
    /// Invalid readings (0 or positive) are discarded.
    /// </summary>
    /// <param name="bluetoothAddress">The device's Bluetooth address.</param>
    /// <param name="rawRssi">The raw RSSI value in dBm.</param>
    /// <returns>The smoothed RSSI value.</returns>
    public double Smooth(ulong bluetoothAddress, double rawRssi)
    {
        // Discard invalid RSSI values (0 or positive are not valid dBm signal readings)
        if (rawRssi >= 0)
        {
            // If we have a previous smoothed value, return it unchanged
            if (_smoothedValues.TryGetValue(bluetoothAddress, out double existing))
            {
                return existing;
            }

            // No previous value exists; discard this reading
            throw new ArgumentException(
                "Invalid RSSI value. Expected a negative dBm value.", nameof(rawRssi));
        }

        var now = DateTime.UtcNow;

        // Check if device should be reset due to OutOfRangeTimeout expiry
        if (_lastSeenTimestamps.TryGetValue(bluetoothAddress, out DateTime lastSeen))
        {
            if (now - lastSeen > _outOfRangeTimeout)
            {
                // Device reappeared after timeout - reset its smoothing state
                _smoothedValues.Remove(bluetoothAddress);
            }
        }

        // Update last seen timestamp
        _lastSeenTimestamps[bluetoothAddress] = now;

        // First valid reading initializes directly
        if (!_smoothedValues.ContainsKey(bluetoothAddress))
        {
            _smoothedValues[bluetoothAddress] = rawRssi;
            return rawRssi;
        }

        // Apply EMA formula: SmoothedRSSI = alpha * NewRSSI + (1 - alpha) * PreviousSmoothedRSSI
        double previous = _smoothedValues[bluetoothAddress];
        double smoothed = _alpha * rawRssi + (1 - _alpha) * previous;
        _smoothedValues[bluetoothAddress] = smoothed;

        return smoothed;
    }

    /// <summary>
    /// Clears the smoothing state for a specific device.
    /// </summary>
    /// <param name="bluetoothAddress">The device's Bluetooth address.</param>
    public void Reset(ulong bluetoothAddress)
    {
        _smoothedValues.Remove(bluetoothAddress);
        _lastSeenTimestamps.Remove(bluetoothAddress);
    }

    /// <summary>
    /// Clears all device smoothing states.
    /// </summary>
    public void ResetAll()
    {
        _smoothedValues.Clear();
        _lastSeenTimestamps.Clear();
    }
}
