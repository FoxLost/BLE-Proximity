using BLEProximity.Helpers;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property 1: BluetoothAddress Formatting
/// Validates: Requirements 1.2
///
/// For any valid 48-bit unsigned integer (ulong) representing a Bluetooth address,
/// formatting it SHALL produce a string of exactly 12 uppercase hexadecimal characters
/// with no separators.
/// </summary>
public class BluetoothAddressFormattingTests
{
    /// <summary>
    /// **Validates: Requirements 1.2**
    /// For any arbitrary ulong value, the formatted result SHALL be exactly 12 characters long.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(BluetoothAddressArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "1: BluetoothAddress Formatting")]
    public Property Format_AlwaysProduces12Characters(BluetoothAddressInput input)
    {
        var result = BluetoothAddressFormatter.Format(input.Value);

        return (result.Length == 12)
            .Label($"Address 0x{input.Value:X} produced '{result}' with length {result.Length}, expected 12");
    }

    /// <summary>
    /// **Validates: Requirements 1.2**
    /// For any arbitrary ulong value, the formatted result SHALL contain only uppercase
    /// hexadecimal characters (0-9, A-F).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(BluetoothAddressArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "1: BluetoothAddress Formatting")]
    public Property Format_ContainsOnlyUppercaseHexCharacters(BluetoothAddressInput input)
    {
        var result = BluetoothAddressFormatter.Format(input.Value);

        var allHex = result.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));

        return allHex
            .Label($"Address 0x{input.Value:X} produced '{result}' which contains non-hex characters");
    }

    /// <summary>
    /// **Validates: Requirements 1.2**
    /// For any arbitrary ulong value, the formatted result SHALL contain no separators
    /// (no colons, dashes, or spaces).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(BluetoothAddressArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "1: BluetoothAddress Formatting")]
    public Property Format_ContainsNoSeparators(BluetoothAddressInput input)
    {
        var result = BluetoothAddressFormatter.Format(input.Value);

        var noSeparators = !result.Contains(':') && !result.Contains('-') && !result.Contains(' ');

        return noSeparators
            .Label($"Address 0x{input.Value:X} produced '{result}' which contains separators");
    }

    /// <summary>
    /// **Validates: Requirements 1.2**
    /// For any arbitrary ulong value, the formatted result SHALL be entirely uppercase
    /// (equal to its own ToUpperInvariant).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(BluetoothAddressArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "1: BluetoothAddress Formatting")]
    public Property Format_ResultIsUppercase(BluetoothAddressInput input)
    {
        var result = BluetoothAddressFormatter.Format(input.Value);

        return (result == result.ToUpperInvariant())
            .Label($"Address 0x{input.Value:X} produced '{result}' which is not fully uppercase");
    }

    /// <summary>
    /// **Validates: Requirements 1.2**
    /// For any arbitrary ulong value, the formatted result SHALL represent the lower 48 bits
    /// of the input (masking with 0xFFFFFFFFFFFF).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(BluetoothAddressArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "1: BluetoothAddress Formatting")]
    public Property Format_RepresentsLower48Bits(BluetoothAddressInput input)
    {
        var result = BluetoothAddressFormatter.Format(input.Value);

        // Parse the result back to verify it matches the lower 48 bits
        var expected = input.Value & 0xFFFFFFFFFFFF;
        var parsed = Convert.ToUInt64(result, 16);

        return (parsed == expected)
            .Label($"Address 0x{input.Value:X} formatted as '{result}' (parsed: 0x{parsed:X}), expected lower 48 bits: 0x{expected:X}");
    }
}

/// <summary>
/// Wrapper type for Bluetooth address input values.
/// </summary>
public record struct BluetoothAddressInput(ulong Value);

/// <summary>
/// Generates arbitrary ulong values for Bluetooth address testing.
/// Includes edge cases (0, max 48-bit, max ulong) and random values.
/// </summary>
public class BluetoothAddressArbitrary
{
    public static Arbitrary<BluetoothAddressInput> BluetoothAddressInput()
    {
        var gen = Gen.Frequency(
            Tuple.Create(5, Gen.Choose(0, int.MaxValue)
                .Two()
                .Select(t => (ulong)(uint)t.Item1 | ((ulong)(uint)t.Item2 << 32))),
            Tuple.Create(2, Gen.Choose(0, int.MaxValue)
                .Select(i => (ulong)(uint)i)),
            Tuple.Create(1, Gen.Elements(
                0UL,
                0xFFFFFFFFFFFFUL,       // Max 48-bit value
                ulong.MaxValue,          // Max ulong (tests masking)
                0x0000000001UL,          // Minimum non-zero
                0xAABBCCDDEEFFUL,        // Typical MAC-like value
                0x112233445566UL,        // Another typical value
                0xFFFF_AABBCCDDEEFFUL,   // High bits set (tests masking)
                0x000000000000UL         // All zeros
            ))
        );

        return Arb.From(gen.Select(v => new BluetoothAddressInput(v)));
    }
}
