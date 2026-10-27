namespace voltrex_light.Models;

/// <summary>
/// Configuration parameters for a scan operation.
/// </summary>
public class ScanOptions
{
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// By default, directory reparse points (junctions and symlinks) are not traversed recursively
    /// to prevent infinite cycles and double-counting storage sizes.
    /// </summary>
    public bool FollowReparsePoints { get; set; } = false;

    /// <summary>
    /// Whether to scan hidden and system files. Defaults to true to reflect real disk consumption.
    /// </summary>
    public bool IncludeHiddenAndSystem { get; set; } = true;

    /// <summary>
    /// Maximum directory depth to traverse (null = unlimited full traversal).
    /// </summary>
    public int? MaxDepth { get; set; } = null;

    /// <summary>
    /// Maximum number of individual largest files to retain in the global heavy file cache.
    /// </summary>
    public int MaxLargestFilesCount { get; set; } = 500;
}

