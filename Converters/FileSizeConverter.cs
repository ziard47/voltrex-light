using System;
using Microsoft.UI.Xaml.Data;
using voltrex_light.Helpers;

namespace voltrex_light.Converters;

/// <summary>
/// Converts numeric byte values to human-readable size strings (e.g. 1.2 GB).
/// </summary>
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long l) return FileSystemFormatter.FormatBytes(l);
        if (value is int i) return FileSystemFormatter.FormatBytes(i);
        if (value is double d) return FileSystemFormatter.FormatBytes((long)d);
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

