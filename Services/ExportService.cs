using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
#if !TESTING
using Windows.Storage.Pickers;
#endif
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Implements RFC 4180 CSV export and hierarchical JSON report generation.
/// </summary>
public class ExportService : IExportService
{
    private IntPtr _windowHandle = IntPtr.Zero;

    public void SetWindowHandle(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public string ExportToCsv(ScanResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var sb = new StringBuilder();

        // Metadata Header Comment
        sb.AppendLine($"# DiskScope Scan Report");
        sb.AppendLine($"# Root: {EscapeCsvField(result.RootPath)}");
        sb.AppendLine($"# Status: {result.Status} (Partial: {result.IsPartial})");
        sb.AppendLine($"# Total Bytes: {result.TotalBytesScanned} ({result.FormattedTotalBytes})");
        sb.AppendLine($"# Total Files: {result.TotalFilesCount}");
        sb.AppendLine($"# Total Folders: {result.TotalDirectoriesCount}");
        sb.AppendLine($"# Skipped Items: {result.SkippedCount}");
        sb.AppendLine($"# Elapsed Time: {result.FormattedElapsedTime}");

        // Table Header
        sb.AppendLine("Name,FullPath,Type,SizeBytes,FormattedSize,PercentOfParent,PercentOfRoot,TotalFiles,TotalSubfolders,LastModified");

        if (result.RootNode != null)
        {
            AppendCsvNodeRecursive(sb, result.RootNode);
        }

        return sb.ToString();
    }

    public string ExportToJson(ScanResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var reportObject = new
        {
            schemaVersion = "1.0",
            application = "DiskScope",
            exportTimestamp = DateTime.UtcNow.ToString("o"),
            scanMetadata = new
            {
                rootPath = result.RootPath,
                status = result.Status.ToString(),
                isPartial = result.IsPartial,
                totalBytesScanned = result.TotalBytesScanned,
                formattedTotalBytes = result.FormattedTotalBytes,
                totalFilesCount = result.TotalFilesCount,
                totalDirectoriesCount = result.TotalDirectoriesCount,
                skippedCount = result.SkippedCount,
                elapsedTimeSeconds = result.ElapsedTime.TotalSeconds,
                warnings = result.Warnings.Select(w => new
                {
                    path = w.Path,
                    warningType = w.WarningType.ToString(),
                    message = w.Message
                }).ToList()
            },
            tree = result.RootNode != null ? ConvertNodeToJsonDto(result.RootNode) : null
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(reportObject, options);
    }

    public async Task<string?> ExportWithDialogAsync(ScanResult result, string format)
    {
#if TESTING
        await Task.CompletedTask;
        return "/mock/path/export." + format;
#else
        try
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            bool isCsv = format.Equals("csv", StringComparison.OrdinalIgnoreCase);
            if (isCsv)
            {
                savePicker.FileTypeChoices.Add("CSV Spreadsheet (*.csv)", new List<string> { ".csv" });
                savePicker.SuggestedFileName = $"DiskScope_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            }
            else
            {
                savePicker.FileTypeChoices.Add("JSON Document (*.json)", new List<string> { ".json" });
                savePicker.SuggestedFileName = $"DiskScope_Report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            }

            if (_windowHandle != IntPtr.Zero)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, _windowHandle);
            }

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                string content = isCsv ? ExportToCsv(result) : ExportToJson(result);
                await File.WriteAllTextAsync(file.Path, content, Encoding.UTF8);
                return file.Path;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExportService] Save failed: {ex.Message}");
        }

        return null;
#endif
    }

    private void AppendCsvNodeRecursive(StringBuilder sb, FileSystemNode node)
    {
        string typeStr = node.IsDirectory ? "Folder" : "File";
        string modifiedStr = node.LastModified == DateTime.MinValue ? "" : node.LastModified.ToString("yyyy-MM-dd HH:mm:ss");

        sb.Append(EscapeCsvField(node.Name)).Append(',');
        sb.Append(EscapeCsvField(node.FullPath)).Append(',');
        sb.Append(typeStr).Append(',');
        sb.Append(node.SizeBytes).Append(',');
        sb.Append(EscapeCsvField(node.FormattedSize)).Append(',');
        sb.Append(node.PercentOfParent.ToString("F2")).Append(',');
        sb.Append(node.PercentOfRoot.ToString("F2")).Append(',');
        sb.Append(node.TotalFileCount).Append(',');
        sb.Append(node.TotalSubfolderCount).Append(',');
        sb.Append(EscapeCsvField(modifiedStr));
        sb.AppendLine();

        foreach (var child in node.Children)
        {
            AppendCsvNodeRecursive(sb, child);
        }
    }

    private static object ConvertNodeToJsonDto(FileSystemNode node)
    {
        return new
        {
            name = node.Name,
            fullPath = node.FullPath,
            isDirectory = node.IsDirectory,
            sizeBytes = node.SizeBytes,
            percentOfParent = Math.Round(node.PercentOfParent, 2),
            percentOfRoot = Math.Round(node.PercentOfRoot, 2),
            directFiles = node.DirectFileCount,
            directSubfolders = node.DirectSubfolderCount,
            totalFiles = node.TotalFileCount,
            totalSubfolders = node.TotalSubfolderCount,
            lastModified = node.LastModified == DateTime.MinValue ? null : node.LastModified.ToString("o"),
            children = node.Children.Select(ConvertNodeToJsonDto).ToList()
        };
    }

    public static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";

        bool mustQuote = field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
        if (mustQuote)
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
