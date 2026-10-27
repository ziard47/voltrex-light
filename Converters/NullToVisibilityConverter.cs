using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace voltrex_light.Converters;

/// <summary>
/// Converts null or empty strings to Collapsed, non-null to Visible (or inverse).
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool hasValue = value != null;
        if (value is string s)
        {
            hasValue = !string.IsNullOrWhiteSpace(s);
        }

        if (parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

