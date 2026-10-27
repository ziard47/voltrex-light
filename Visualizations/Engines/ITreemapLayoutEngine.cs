using System.Collections.Generic;
using voltrex_light.Models;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Visualizations.Engines;

/// <summary>
/// Interface for computing treemap geometry from a file system hierarchy.
/// Independent of UI framework.
/// </summary>
public interface ITreemapLayoutEngine
{
    /// <summary>
    /// Computes treemap rectangles for the given root node within the specified bounds.
    /// </summary>
    IReadOnlyList<TreemapItem> ComputeLayout(
        FileSystemNode rootNode,
        double width,
        double height,
        int maxDepth = 2);

    /// <summary>
    /// Performs hit testing to find the innermost item at the specified coordinate.
    /// </summary>
    TreemapItem? HitTest(IReadOnlyList<TreemapItem> items, double x, double y);
}

