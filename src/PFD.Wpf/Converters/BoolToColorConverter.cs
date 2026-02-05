using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PFD.Wpf.Converters;

public class BoolToColorConverter : IValueConverter
{
    public Brush? TrueColor { get; set; }
    public Brush? FalseColor { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueColor : FalseColor;
        }
        return FalseColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToStrikethroughConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue)
        {
            return TextDecorations.Strikethrough;
        }
        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DayIndicatorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return new SolidColorBrush(Color.FromRgb(0x00, 0x96, 0x88)); // Teal for active day
        }
        return new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)); // Light gray for inactive
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DayTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return Brushes.White;
        }
        return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)); // Gray text for inactive
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class AiButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool showPanel && showPanel)
        {
            return "Hide Insights";
        }
        return "Show Insights";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PriorityScoreToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int score)
        {
            return score switch
            {
                >= 90 => new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F)), // Red - Critical
                >= 70 => new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00)), // Orange - High
                >= 50 => new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)), // Blue - Medium
                _ => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75))       // Gray - Low
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan time)
        {
            var dt = DateTime.Today.Add(time);
            return dt.ToString("h:mm tt");
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && TimeSpan.TryParse(str, out var time))
        {
            return time;
        }
        return null!;
    }
}

public class NullableTimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan time)
        {
            var dt = DateTime.Today.Add(time);
            return dt.ToString("h:mm tt");
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && TimeSpan.TryParse(str, out var time))
        {
            return time;
        }
        return null!;
    }
}

public class HasScheduledTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TimeSpan;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DurationToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int minutes && minutes != 30)
        {
            if (minutes >= 60)
            {
                var hours = minutes / 60;
                var mins = minutes % 60;
                return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
            }
            return $"{minutes}m";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ImportButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isImporting && isImporting)
        {
            return "Importing...";
        }
        return "Import Events";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
