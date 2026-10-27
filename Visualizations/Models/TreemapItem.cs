using System;
using voltrex_light.Models;

namespace voltrex_light.Visualizations.Models;

/// <summary>
/// Layout geometry and display attributes for an individual treemap rectangle.
/// Pure model, decoupled from UI framework.
/// </summary>
public class TreemapItem
{
    public FileSystemNode Node { get; init; } = null!;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int Depth { get; set; }
    public bool IsLeaf { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";

    public double Area => Math.Max(0, Width * Height);
    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>
    /// Determines whether rectangle is large enough to render readable text.
    /// </summary>
    public bool CanRenderLabel => Width >= 56 && Height >= 26;

    /// <summary>
    /// Tests if a screen coordinate falls within this rectangle.
    /// </summary>
    public bool HitTest(double px, double py)
    {
        return px >= X && px <= Right && py >= Y && py <= Bottom;
    }
}

