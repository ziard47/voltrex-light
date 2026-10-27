using voltrex_light.Models;

namespace voltrex_light.Visualizations.Models;

/// <summary>
/// An item in the visualization drilldown breadcrumb trail.
/// </summary>
public class VisualizationBreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public FileSystemNode Node { get; set; } = null!;
    public bool IsFirst { get; set; }
    public bool IsCurrent { get; set; }
    public bool ShowSeparator => !IsFirst;
}

