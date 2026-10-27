using System;

namespace voltrex_light.Helpers;

/// <summary>
/// Unit standards for displaying file sizes.
/// </summary>
public enum SizeUnitFormat
{
    Standard, // 1024 base: B, KB, MB, GB, TB
    Binary    // IEC standard: B, KiB, MiB, GiB, TiB
}

/// <summary>
/// Provides pure utility functions for formatting file sizes, dates, and metrics.
/// Completely decoupled and unit-testable.
/// </summary>
public static class FileSystemFormatter
{
    private static readonly string[] StandardSuffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
    private static readonly string[] BinarySuffixes = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };

    public static SizeUnitFormat ActiveUnitFormat { get; set; } = SizeUnitFormat.Standard;

    /// <summary>
    /// Formats a byte length into a human-readable string (e.g. 1.25 GB, 450 KB or 1.25 GiB, 450 KiB).
    /// </summary>
    public static string FormatBytes(long bytes, int decimalPlaces = 2, SizeUnitFormat? formatOverride = null)
    {
        if (bytes < 0)
        {
            return "-" + FormatBytes(-bytes, decimalPlaces, formatOverride);
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        var format = formatOverride ?? ActiveUnitFormat;
        var suffixes = format == SizeUnitFormat.Binary ? BinarySuffixes : StandardSuffixes;

        int mag = (int)Math.Log(bytes, 1024);
        if (mag >= suffixes.Length)
        {
            mag = suffixes.Length - 1;
        }

        decimal adjustedSize = (decimal)bytes / (1L << (mag * 10));

        if (mag == 0)
        {
            return $"{bytes} B";
        }

        string fmt = "{0:N" + Math.Max(0, decimalPlaces) + "} {1}";
        return string.Format(fmt, adjustedSize, suffixes[mag]);
    }

    /// <summary>
    /// Formats a ratio [0.0 - 1.0] or percentage [0 - 100] as a percentage string (e.g. "45.2%").
    /// </summary>
    public static string FormatPercentage(double ratio, bool isRatio0To1 = true, int decimals = 1)
    {
        double pct = isRatio0To1 ? ratio * 100.0 : ratio;
        if (double.IsNaN(pct) || double.IsInfinity(pct))
        {
            return "0.0%";
        }

        string format = "{0:F" + decimals + "}%";
        return string.Format(format, Math.Clamp(pct, 0.0, 100.0));
    }

    /// <summary>
    /// Formats count of items (e.g. "1,234 files").
    /// </summary>
    public static string FormatCount(long count, string singular, string plural)
    {
        return count == 1 ? $"1 {singular}" : $"{count:N0} {plural}";
    }
}

