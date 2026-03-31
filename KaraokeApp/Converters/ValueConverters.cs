using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KaraokeApp.Converters
{
    /// <summary>bool → Visibility (true = Visible)</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>bool → Visibility (true = Collapsed, i.e. inverse)</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => !(value is Visibility v && v == Visibility.Visible);
    }

    /// <summary>bool → "ON" / "OFF"</summary>
    public class BoolToOnOffConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? "ON" : "OFF";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value?.ToString() == "ON";
    }

    /// <summary>Toggle button active/inactive background colour</summary>
    public class BoolToActiveBrushConverter : IValueConverter
    {
        public Brush ActiveBrush   { get; set; } = new SolidColorBrush(Color.FromRgb(80, 140, 220));
        public Brush InactiveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(200, 200, 200));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? ActiveBrush : InactiveBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>int pitch semitones → display string (e.g., "+2", "0", "-3")</summary>
    public class PitchToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return i > 0 ? $"+{i}" : i.ToString();
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => int.TryParse(value?.ToString()?.TrimStart('+'), out int r) ? r : 0;
    }
}
