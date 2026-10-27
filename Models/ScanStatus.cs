namespace voltrex_light.Models;

/// <summary>
/// Execution states for a disk or folder scan.
/// </summary>
public enum ScanStatus
{
    Idle,
    Preparing,
    Scanning,
    Finalizing,
    Completed,
    CompletedWithWarnings,
    Cancelled,
    Failed
}

