namespace BLEProximity.Helpers;

/// <summary>
/// Represents the result of threshold validation, including whether the values are valid
/// and an error message for UI display when constraints are violated.
/// </summary>
public class ThresholdValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ThresholdValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ThresholdValidationResult Success() => new(true, null);

    public static ThresholdValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
