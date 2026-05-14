using BLEProximity.Services;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property 12: Command Placeholder Substitution
/// Validates: Requirements 6.3
///
/// For any command argument string containing placeholders ({mac}, {name}, {rssi}, {timestamp})
/// and any valid DeviceContext, after substitution the resulting string SHALL contain no unresolved
/// placeholder tokens, and each placeholder SHALL be replaced with the corresponding device context value.
/// </summary>
public class CommandPlaceholderTests
{
    /// <summary>
    /// **Validates: Requirements 6.3**
    /// For any argument string containing placeholder tokens and any valid DeviceContext,
    /// after substitution no unresolved placeholder tokens remain in the result.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PlaceholderArgsArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "12: Command Placeholder Substitution")]
    public Property AfterSubstitution_NoUnresolvedPlaceholdersRemain(PlaceholderInput input)
    {
        var result = CommandExecutor.SubstitutePlaceholders(input.Arguments, input.Context);

        var noMac = !result.Contains("{mac}", StringComparison.OrdinalIgnoreCase);
        var noName = !result.Contains("{name}", StringComparison.OrdinalIgnoreCase);
        var noRssi = !result.Contains("{rssi}", StringComparison.OrdinalIgnoreCase);
        var noTimestamp = !result.Contains("{timestamp}", StringComparison.OrdinalIgnoreCase);

        return (noMac && noName && noRssi && noTimestamp)
            .Label($"Unresolved placeholders found in result: '{result}' (from args: '{input.Arguments}')");
    }

    /// <summary>
    /// **Validates: Requirements 6.3**
    /// For any argument string containing {mac} and any valid DeviceContext,
    /// after substitution the result contains the DeviceContext.MacAddress value.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(MacPlaceholderArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "12: Command Placeholder Substitution")]
    public Property MacPlaceholder_ReplacedWithMacAddress(MacPlaceholderInput input)
    {
        var result = CommandExecutor.SubstitutePlaceholders(input.Arguments, input.Context);

        return result.Contains(input.Context.MacAddress)
            .Label($"Expected MAC '{input.Context.MacAddress}' in result '{result}'");
    }

    /// <summary>
    /// **Validates: Requirements 6.3**
    /// For any argument string containing {name} and any valid DeviceContext,
    /// after substitution the result contains the DeviceContext.Name value.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(NamePlaceholderArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "12: Command Placeholder Substitution")]
    public Property NamePlaceholder_ReplacedWithDeviceName(NamePlaceholderInput input)
    {
        var result = CommandExecutor.SubstitutePlaceholders(input.Arguments, input.Context);

        return result.Contains(input.Context.Name)
            .Label($"Expected Name '{input.Context.Name}' in result '{result}'");
    }

    /// <summary>
    /// **Validates: Requirements 6.3**
    /// For any argument string containing {rssi} and any valid DeviceContext,
    /// after substitution the result contains the formatted SmoothedRssi value.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(RssiPlaceholderArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "12: Command Placeholder Substitution")]
    public Property RssiPlaceholder_ReplacedWithSmoothedRssi(RssiPlaceholderInput input)
    {
        var result = CommandExecutor.SubstitutePlaceholders(input.Arguments, input.Context);
        var expectedRssi = input.Context.SmoothedRssi.ToString("F1");

        return result.Contains(expectedRssi)
            .Label($"Expected RSSI '{expectedRssi}' in result '{result}'");
    }

    /// <summary>
    /// **Validates: Requirements 6.3**
    /// For any argument string containing {timestamp} and any valid DeviceContext,
    /// after substitution the result contains the ISO 8601 formatted timestamp.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(TimestampPlaceholderArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "12: Command Placeholder Substitution")]
    public Property TimestampPlaceholder_ReplacedWithIso8601Timestamp(TimestampPlaceholderInput input)
    {
        var result = CommandExecutor.SubstitutePlaceholders(input.Arguments, input.Context);
        var expectedTimestamp = input.Context.Timestamp.ToString("o");

        return result.Contains(expectedTimestamp)
            .Label($"Expected Timestamp '{expectedTimestamp}' in result '{result}'");
    }

    /// <summary>
    /// **Validates: Requirements 6.3**
    /// For any argument string containing all four placeholders and any valid DeviceContext,
    /// after substitution all four values are present in the result.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AllPlaceholdersArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "12: Command Placeholder Substitution")]
    public Property AllPlaceholders_AllReplacedCorrectly(AllPlaceholdersInput input)
    {
        var result = CommandExecutor.SubstitutePlaceholders(input.Arguments, input.Context);

        var expectedRssi = input.Context.SmoothedRssi.ToString("F1");
        var expectedTimestamp = input.Context.Timestamp.ToString("o");

        var containsMac = result.Contains(input.Context.MacAddress);
        var containsName = result.Contains(input.Context.Name);
        var containsRssi = result.Contains(expectedRssi);
        var containsTimestamp = result.Contains(expectedTimestamp);

        return (containsMac && containsName && containsRssi && containsTimestamp)
            .Label($"Not all values found in result '{result}'. " +
                   $"MAC={containsMac}, Name={containsName}, RSSI={containsRssi}, Timestamp={containsTimestamp}");
    }
}

// Input wrapper types

public record struct PlaceholderInput(string Arguments, DeviceContext Context);
public record struct MacPlaceholderInput(string Arguments, DeviceContext Context);
public record struct NamePlaceholderInput(string Arguments, DeviceContext Context);
public record struct RssiPlaceholderInput(string Arguments, DeviceContext Context);
public record struct TimestampPlaceholderInput(string Arguments, DeviceContext Context);
public record struct AllPlaceholdersInput(string Arguments, DeviceContext Context);

// Helper to generate safe DeviceContext values that don't contain placeholder tokens

internal static class DeviceContextGenerator
{
    /// <summary>
    /// Generates a safe string that does not contain any placeholder tokens.
    /// Uses alphanumeric characters only to avoid false positives.
    /// </summary>
    public static Gen<string> SafeString(int minLength = 1, int maxLength = 20)
    {
        return Gen.Choose(minLength, maxLength).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements(
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            .Select(chars => new string(chars)));
    }

    /// <summary>
    /// Generates a MAC address string (12 uppercase hex chars) that cannot contain placeholder tokens.
    /// </summary>
    public static Gen<string> MacAddress()
    {
        return Gen.ArrayOf(12, Gen.Elements(
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F'))
            .Select(chars => new string(chars));
    }

    /// <summary>
    /// Generates a valid RSSI value (negative dBm).
    /// </summary>
    public static Gen<double> Rssi()
    {
        return Gen.Choose(-1200, -100).Select(i => i / 10.0); // [-120.0, -10.0]
    }

    /// <summary>
    /// Generates a valid DateTime for timestamps.
    /// </summary>
    public static Gen<DateTime> Timestamp()
    {
        return Gen.Choose(2020, 2030).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).SelectMany(day =>
                    Gen.Choose(0, 23).SelectMany(hour =>
                        Gen.Choose(0, 59).SelectMany(minute =>
                            Gen.Choose(0, 59).Select(second =>
                                new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc)))))));
    }

    /// <summary>
    /// Generates a DeviceContext with safe values that don't contain placeholder tokens.
    /// </summary>
    public static Gen<DeviceContext> Context()
    {
        return MacAddress().SelectMany(mac =>
            SafeString(3, 15).SelectMany(name =>
                Rssi().SelectMany(rssi =>
                    Timestamp().Select(ts => new DeviceContext
                    {
                        MacAddress = mac,
                        Name = name,
                        SmoothedRssi = rssi,
                        Timestamp = ts
                    }))));
    }
}

/// <summary>
/// Generates argument strings with various combinations of placeholders.
/// </summary>
public class PlaceholderArgsArbitrary
{
    public static Arbitrary<PlaceholderInput> PlaceholderInput()
    {
        var placeholders = new[] { "{mac}", "{name}", "{rssi}", "{timestamp}" };

        var argsGen = Gen.Choose(1, 4).SelectMany(count =>
            Gen.ArrayOf(count, Gen.Elements(placeholders)).SelectMany(selected =>
                DeviceContextGenerator.SafeString(0, 10).SelectMany(prefix =>
                    DeviceContextGenerator.SafeString(0, 10).Select(suffix =>
                        prefix + " " + string.Join(" ", selected) + " " + suffix))));

        var gen = argsGen.SelectMany(args =>
            DeviceContextGenerator.Context().Select(ctx =>
                new PlaceholderInput(args, ctx)));

        return Arb.From(gen);
    }
}

/// <summary>
/// Generates argument strings containing {mac} placeholder.
/// </summary>
public class MacPlaceholderArbitrary
{
    public static Arbitrary<MacPlaceholderInput> MacPlaceholderInput()
    {
        var argsGen = DeviceContextGenerator.SafeString(0, 10).SelectMany(prefix =>
            DeviceContextGenerator.SafeString(0, 10).Select(suffix =>
                prefix + "{mac}" + suffix));

        var gen = argsGen.SelectMany(args =>
            DeviceContextGenerator.Context().Select(ctx =>
                new MacPlaceholderInput(args, ctx)));

        return Arb.From(gen);
    }
}

/// <summary>
/// Generates argument strings containing {name} placeholder.
/// </summary>
public class NamePlaceholderArbitrary
{
    public static Arbitrary<NamePlaceholderInput> NamePlaceholderInput()
    {
        var argsGen = DeviceContextGenerator.SafeString(0, 10).SelectMany(prefix =>
            DeviceContextGenerator.SafeString(0, 10).Select(suffix =>
                prefix + "{name}" + suffix));

        var gen = argsGen.SelectMany(args =>
            DeviceContextGenerator.Context().Select(ctx =>
                new NamePlaceholderInput(args, ctx)));

        return Arb.From(gen);
    }
}

/// <summary>
/// Generates argument strings containing {rssi} placeholder.
/// </summary>
public class RssiPlaceholderArbitrary
{
    public static Arbitrary<RssiPlaceholderInput> RssiPlaceholderInput()
    {
        var argsGen = DeviceContextGenerator.SafeString(0, 10).SelectMany(prefix =>
            DeviceContextGenerator.SafeString(0, 10).Select(suffix =>
                prefix + "{rssi}" + suffix));

        var gen = argsGen.SelectMany(args =>
            DeviceContextGenerator.Context().Select(ctx =>
                new RssiPlaceholderInput(args, ctx)));

        return Arb.From(gen);
    }
}

/// <summary>
/// Generates argument strings containing {timestamp} placeholder.
/// </summary>
public class TimestampPlaceholderArbitrary
{
    public static Arbitrary<TimestampPlaceholderInput> TimestampPlaceholderInput()
    {
        var argsGen = DeviceContextGenerator.SafeString(0, 10).SelectMany(prefix =>
            DeviceContextGenerator.SafeString(0, 10).Select(suffix =>
                prefix + "{timestamp}" + suffix));

        var gen = argsGen.SelectMany(args =>
            DeviceContextGenerator.Context().Select(ctx =>
                new TimestampPlaceholderInput(args, ctx)));

        return Arb.From(gen);
    }
}

/// <summary>
/// Generates argument strings containing all four placeholders.
/// </summary>
public class AllPlaceholdersArbitrary
{
    public static Arbitrary<AllPlaceholdersInput> AllPlaceholdersInput()
    {
        var argsGen = DeviceContextGenerator.SafeString(0, 5).SelectMany(p1 =>
            DeviceContextGenerator.SafeString(0, 5).SelectMany(p2 =>
                DeviceContextGenerator.SafeString(0, 5).SelectMany(p3 =>
                    DeviceContextGenerator.SafeString(0, 5).SelectMany(p4 =>
                        DeviceContextGenerator.SafeString(0, 5).Select(p5 =>
                            p1 + "{mac}" + p2 + "{name}" + p3 + "{rssi}" + p4 + "{timestamp}" + p5)))));

        var gen = argsGen.SelectMany(args =>
            DeviceContextGenerator.Context().Select(ctx =>
                new AllPlaceholdersInput(args, ctx)));

        return Arb.From(gen);
    }
}
