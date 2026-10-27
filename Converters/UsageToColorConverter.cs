using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace voltrex_light.Converters;

/// <summary>
/// Converts a drive usage percentage into an appropriate visual status color brush:
/// > 90% => Critical Red
/// > 80% => Warning Amber/Orange
/// Default => Standard Accent Color / Info Blue
/// </summary>
public class UsageToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush NormalBrush = new(Color.FromArgb(255, 0, 120, 215)); // Windows Blue
    private static readonly SolidColorBrush WarningBrush = new(Color.FromArgb(255, 230, 140, 10)); // Warning Amber
    private static readonly SolidColorBrush CriticalBrush = new(Color.FromArgb(255, 232, 17, 35)); // Critical Red

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double pct = 0;
        if (value is double d) pct = d;
        else if (value is float f) pct = f;
        else if (value is int i) pct = i;

        if (pct >= 90.0) return CriticalBrush;
        if (pct >= 80.0) return WarningBrush;
        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

