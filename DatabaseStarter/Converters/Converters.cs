using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DatabaseStarter.Models;

namespace DatabaseStarter.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DatabaseStatus status)
        {
            return status switch
            {
                DatabaseStatus.NotInstalled => new SolidColorBrush(Color.FromRgb(158, 158, 158)),  // Gray
                DatabaseStatus.Installed => new SolidColorBrush(Color.FromRgb(33, 150, 243)),       // Blue
                DatabaseStatus.Running => new SolidColorBrush(Color.FromRgb(76, 175, 80)),          // Green
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

