using System;
using voltrex_light.Models;

namespace voltrex_light.Visualizations.Models;

/// <summary>
/// Layout geometry and display attributes for a radial sunburst wedge or center circle.
/// Pure model, decoupled from UI framework.
/// </summary>
public class SunburstSlice
{
    public FileSystemNode Node { get; init; } = null!;
    public double InnerRadius { get; set; }
    public double OuterRadius { get; set; }
    public double StartAngle { get; set; } // in Radians [0, 2*PI]
    public double EndAngle { get; set; }   // in Radians [0, 2*PI]
    public int Depth { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";

    public double SweepAngle => Math.Max(0, EndAngle - StartAngle);

    /// <summary>
    /// Determines whether the annular sector is wide enough to display text.
    /// </summary>
    public bool CanRenderLabel => (Depth == 0 && OuterRadius >= 28) ||
                                  (SweepAngle >= 0.40 && (OuterRadius - InnerRadius) >= 20 && ((InnerRadius + OuterRadius) / 2.0 * SweepAngle) >= 44);

    /// <summary>
    /// Tests if a point relative to the chart center (px, py) falls inside this annular sector.
    /// </summary>
    public bool HitTest(double px, double py, double centerX, double centerY)
    {
        double dx = px - centerX;
        double dy = py - centerY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < InnerRadius || distance > OuterRadius)
        {
            return false;
        }

        // Center circle (depth 0)
        if (Depth == 0 || InnerRadius == 0)
        {
            return distance <= OuterRadius;
        }

        // Calculate angle in radians [0, 2*PI)
        double angle = Math.Atan2(dy, dx);
        if (angle < 0)
        {
            angle += 2.0 * Math.PI;
        }

        return angle >= StartAngle && angle <= EndAngle;
    }
}

