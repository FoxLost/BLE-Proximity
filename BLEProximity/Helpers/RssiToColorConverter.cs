using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BLEProximity.Helpers;

/// <summary>
/// Converts a smoothed RSSI value (double) to a SolidColorBrush for DataGrid row background.
/// Green (> -70 dBm), Orange ([-80, -70] dBm), Red (< -80 dBm), Transparent (null/uninitialized/0).
/// </summary>
public class RssiToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromArgb(80, 76, 175, 80));
    private static readonly SolidColorBrush OrangeBrush = new(Color.FromArgb(80, 255, 152, 0));
    private static readonly SolidColorBrush RedBrush = new(Color.FromArgb(80, 244, 67, 54));
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    static RssiToColorConverter()
    {
        GreenBrush.Freeze();
        OrangeBrush.Freeze();
        RedBrush.Freeze();
        TransparentBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double rssi || rssi == 0.0)
            return TransparentBrush;

        var category = RssiColorClassifier.Classify(rssi);

        return category switch
        {
            RssiColorCategory.Green => GreenBrush,
            RssiColorCategory.Orange => OrangeBrush,
            RssiColorCategory.Red => RedBrush,
            _ => TransparentBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
