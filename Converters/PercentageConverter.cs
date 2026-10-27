using System;
using Microsoft.UI.Xaml.Data;
using voltrex_light.Helpers;

namespace voltrex_light.Converters;

/// <summary>
/// Converts numeric ratio or percentage to formatted percentage string.
/// </summary>
public class PercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return FileSystemFormatter.FormatPercentage(d, isRatio0To1: false);
        }
        return "0.0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

