using System;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Service interface for exporting disk analysis scan results to CSV and JSON formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Configures the parent HWND for native Windows save picker dialogs.
    /// </summary>
    void SetWindowHandle(IntPtr windowHandle);

    /// <summary>
    /// Generates a RFC 4180 compliant CSV string of the scanned tree.
    /// </summary>
    string ExportToCsv(ScanResult result);

    /// <summary>
    /// Generates a structured JSON string of the scan metadata and hierarchy.
    /// </summary>
    string ExportToJson(ScanResult result);

    /// <summary>
    /// Prompts the user with a native Windows save file picker and saves the report.
    /// </summary>
    Task<string?> ExportWithDialogAsync(ScanResult result, string format); // "csv" or "json"
}
