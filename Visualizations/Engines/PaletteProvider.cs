using System;

namespace voltrex_light.Visualizations.Engines;

/// <summary>
/// Provides cohesive, accessible, theme-respecting color assignment for disk visualization items.
/// Assigns distinct hues by branch while keeping children in complementary tints.
/// </summary>
public static class PaletteProvider
{
    private static readonly string[] BasePalette =
    {
        "#2563EB", // Blue
        "#059669", // Emerald
        "#D97706", // Amber
        "#7C3AED", // Violet
        "#0891B2", // Cyan
        "#DC2626", // Red
        "#4B5563", // Gray / Neutral
        "#4F46E5", // Indigo
        "#EA580C", // Orange
        "#0D9488", // Teal
        "#9333EA", // Purple
        "#CA8A04"  // Yellow-Brown
    };

    public static string GetColorForNode(int branchIndex, int depth, bool isDirectory)
    {
        int paletteIndex = Math.Abs(branchIndex) % BasePalette.Length;
        string baseColor = BasePalette[paletteIndex];

        if (!isDirectory)
        {
            // Slightly softer neutral for individual files
            return AdjustBrightness(baseColor, 0.85);
        }

        if (depth <= 1)
        {
            return baseColor;
        }

        // Slightly adjust tone for deeper levels
        double factor = Math.Max(0.65, 1.0 - (depth - 1) * 0.1);
        return AdjustBrightness(baseColor, factor);
    }

    private static string AdjustBrightness(string hexColor, double factor)
    {
        if (hexColor.StartsWith('#')) hexColor = hexColor[1..];
        if (hexColor.Length != 6) return "#2563EB";

        int r = Convert.ToInt32(hexColor[..2], 16);
        int g = Convert.ToInt32(hexColor[2..4], 16);
        int b = Convert.ToInt32(hexColor[4..6], 16);

        r = Math.Clamp((int)(r * factor), 0, 255);
        g = Math.Clamp((int)(g * factor), 0, 255);
        b = Math.Clamp((int)(b * factor), 0, 255);

        return $"#{r:X2}{g:X2}{b:X2}";
    }
}

