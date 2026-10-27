using System;

namespace voltrex_light.Models;

/// <summary>
/// Types of warnings or reasons for skipping items during a disk scan.
/// </summary>
public enum ScanWarningType
{
    UnauthorizedAccess,
    ReparsePointSkipped,
    PathTooLong,
    FileNotFound,
    DirectoryNotFound,
    LockedFile,
    DeviceError,
    GeneralIoError
}

/// <summary>
/// Details of an inaccessible or skipped path encountered during scanning.
/// </summary>
public class ScanWarning
{
    public string Path { get; set; } = string.Empty;
    public ScanWarningType WarningType { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string TypeDescription => WarningType switch
    {
        ScanWarningType.UnauthorizedAccess => "Access Denied (Requires Elevation / Permission)",
        ScanWarningType.ReparsePointSkipped => "Directory Junction / Symbolic Link (Cycle Prevention)",
        ScanWarningType.PathTooLong => "Path Exceeds System Limits",
        ScanWarningType.FileNotFound => "File Removed During Scan",
        ScanWarningType.DirectoryNotFound => "Directory Removed During Scan",
        ScanWarningType.LockedFile => "File Locked by Another Process",
        ScanWarningType.DeviceError => "Hardware / Storage Device I/O Failure",
        _ => "I/O Error"
    };
}

