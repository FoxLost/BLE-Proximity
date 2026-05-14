using System.Globalization;
using System.Windows.Data;

namespace BLEProximity.Helpers;

public class UtcToLocalTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime || dateTime == default)
            return string.Empty;

        var utc = dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

        return utc.ToLocalTime().ToString("HH:mm:ss", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
