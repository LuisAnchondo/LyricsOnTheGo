using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LyricsOnTheGo;

/// <summary>Converts a "#RRGGBB" hex string to a SolidColorBrush for colour swatches.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch { /* invalid hex while typing */ }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
