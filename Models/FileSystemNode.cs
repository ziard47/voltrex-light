using System;
using System.Collections.ObjectModel;
using System.IO;
using voltrex_light.Helpers;

namespace voltrex_light.Models;

/// <summary>
/// Represents a folder or file in the analysis tree and table with 64-bit metrics.
/// </summary>
public class FileSystemNode : ObservableObject
{
    private bool _isExpanded;
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public long? AllocatedSizeBytes { get; set; }

    public double PercentOfParent { get; set; }
    public double PercentOfRoot { get; set; }

    public int DirectFileCount { get; set; }
    public int DirectSubfolderCount { get; set; }

    public long TotalFileCount { get; set; }
    public long TotalSubfolderCount { get; set; }

    // Compatibility aliases for existing UI bindings
    public int FileCount
    {
        get => (int)Math.Min(int.MaxValue, TotalFileCount);
        set => TotalFileCount = value;
    }

    public int SubfolderCount
    {
        get => (int)Math.Min(int.MaxValue, TotalSubfolderCount);
        set => TotalSubfolderCount = value;
    }

    public DateTime LastModified { get; set; }
    public string Extension { get; set; } = string.Empty;
    public FileAttributes Attributes { get; set; }
    public bool IsReparsePoint { get; set; }

    public FileSystemNode? Parent { get; set; }
    public ObservableCollection<FileSystemNode> Children { get; set; } = new();

    public string FormattedSize => FileSystemFormatter.FormatBytes(SizeBytes);
    public string FormattedPercent => FileSystemFormatter.FormatPercentage(PercentOfParent, isRatio0To1: false);
    public string FormattedPercentOfRoot => FileSystemFormatter.FormatPercentage(PercentOfRoot, isRatio0To1: false);
    public string FormattedLastModified => LastModified == DateTime.MinValue ? "—" : LastModified.ToString("yyyy-MM-dd HH:mm");
    public string FormattedFileCount => IsDirectory ? TotalFileCount.ToString("N0") : "—";
    public string FormattedSubfolderCount => IsDirectory ? TotalSubfolderCount.ToString("N0") : "—";
    public string DisplayType => IsDirectory ? "File folder" : (string.IsNullOrEmpty(Extension) ? "File" : $"{Extension.TrimStart('.').ToUpperInvariant()} File");

    public string IconGlyph
    {
        get
        {
            if (IsReparsePoint)
            {
                return "\uE71B"; // Link glyph
            }

            if (IsDirectory)
            {
                return IsExpanded ? "\uE838" : "\uE8B7"; // OpenFolder or Folder
            }

            return GetFileIconGlyph(Extension);
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(IconGlyph));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string GetFileIconGlyph(string ext)
    {
        var cleanExt = ext.TrimStart('.').ToLowerInvariant();
        return cleanExt switch
        {
            "exe" or "msi" or "bat" or "cmd" or "ps1" => "\uE7B5", // Application
            "zip" or "7z" or "rar" or "tar" or "gz" => "\uF012",   // Archive
            "png" or "jpg" or "jpeg" or "gif" or "bmp" or "svg" => "\uEB9F", // Image
            "mp4" or "mkv" or "avi" or "mov" or "wmv" => "\uE714", // Video
            "mp3" or "wav" or "flac" or "aac" or "ogg" => "\uE8D6", // Audio
            "iso" or "vhd" or "vhdx" or "img" => "\uEDA2",        // Disk image
            "txt" or "log" or "md" or "json" or "xml" => "\uE8A5", // Document
            "dll" or "sys" => "\uE90F",                           // System / Component
            _ => "\uE8A5"                                         // Generic document
        };
    }
}

