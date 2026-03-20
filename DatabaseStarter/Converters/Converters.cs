using System.Globalization;
using System.Windows;
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
                DatabaseStatus.NotInstalled => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                DatabaseStatus.Installed => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                DatabaseStatus.Running => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
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

public class EngineToAccentBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };

        if (value is DatabaseEngine engine)
        {
            switch (engine)
            {
                case DatabaseEngine.MySQL:
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 119, 182), 0));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 180, 216), 1));
                    break;
                case DatabaseEngine.MariaDB:
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(194, 24, 91), 0));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(240, 98, 146), 1));
                    break;
                case DatabaseEngine.PostgreSQL:
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(57, 73, 171), 0));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(121, 134, 203), 1));
                    break;
                default:
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(108, 112, 134), 0));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(147, 153, 178), 1));
                    break;
            }
        }

        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
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