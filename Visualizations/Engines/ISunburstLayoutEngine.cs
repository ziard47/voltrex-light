using System.Collections.Generic;
using voltrex_light.Models;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Visualizations.Engines;

/// <summary>
/// Interface for computing Filelight-style radial sunburst wedges from a file system hierarchy.
/// Independent of UI framework.
/// </summary>
public interface ISunburstLayoutEngine
{
    /// <summary>
    /// Computes concentric annular wedges for the visualization root.
    /// </summary>
    IReadOnlyList<SunburstSlice> ComputeLayout(
        FileSystemNode rootNode,
        double availableWidth,
        double availableHeight,
        int maxDepth = 4,
        double minAngleThreshold = 0.015); // ~0.86 degrees min threshold

    /// <summary>
    /// Performs polar hit testing from Cartesian coordinates.
    /// </summary>
    SunburstSlice? HitTest(
        IReadOnlyList<SunburstSlice> slices,
        double px,
        double py,
        double centerX,
        double centerY);
}

