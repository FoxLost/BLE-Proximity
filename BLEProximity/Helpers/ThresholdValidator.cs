namespace BLEProximity.Helpers;

/// <summary>
/// Validates InRangeThreshold and OutOfRangeThreshold values to ensure they meet
/// the hysteresis buffer and range constraints defined in Requirements 3.1, 3.2, 3.5.
/// </summary>
public static class ThresholdValidator
{
    private const int InRangeMin = -90;
    private const int InRangeMax = -50;
    private const int OutOfRangeMin = -95;
    private const int OutOfRangeMax = -55;
    private const int MinimumBuffer = 5;

    /// <summary>
    /// Validates the given threshold pair against all constraints.
    /// </summary>
    /// <param name="inRangeThreshold">The InRangeThreshold value in dBm.</param>
    /// <param name="outOfRangeThreshold">The OutOfRangeThreshold value in dBm.</param>
    /// <returns>A <see cref="ThresholdValidationResult"/> indicating success or failure with an error message.</returns>
    public static ThresholdValidationResult Validate(int inRangeThreshold, int outOfRangeThreshold)
    {
        if (inRangeThreshold < InRangeMin || inRangeThreshold > InRangeMax)
        {
            return ThresholdValidationResult.Failure(
                $"InRangeThreshold must be between {InRangeMax} and {InRangeMin} dBm.");
        }

        if (outOfRangeThreshold < OutOfRangeMin || outOfRangeThreshold > OutOfRangeMax)
        {
            return ThresholdValidationResult.Failure(
                $"OutOfRangeThreshold must be between {OutOfRangeMax} and {OutOfRangeMin} dBm.");
        }

        if (outOfRangeThreshold >= inRangeThreshold)
        {
            return ThresholdValidationResult.Failure(
                "OutOfRangeThreshold must be strictly less than InRangeThreshold.");
        }

        if (inRangeThreshold - outOfRangeThreshold < MinimumBuffer)
        {
            return ThresholdValidationResult.Failure(
                $"The buffer between InRangeThreshold and OutOfRangeThreshold must be at least {MinimumBuffer} dBm.");
        }

        return ThresholdValidationResult.Success();
    }
}
