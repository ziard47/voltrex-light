using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// High-performance, production-quality disk scanning engine for Windows file systems.
/// Handles permissions, cancellation, reparse points, throttled progress, and deep aggregation.
/// </summary>
public class ScanService : IScanService
{
    private readonly object _lock = new();
    private CancellationTokenSource? _activeCts;
    private Guid _activeScanId = Guid.Empty;

    public bool IsScanning => Progress.IsScanning;
    public ScanProgressInfo Progress { get; } = new();
    public ScanResult? LastResult { get; private set; }

    public event EventHandler<ScanResult>? ScanCompleted;

    public Task<FileSystemNode?> ScanPathAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        return ScanAsync(new ScanOptions { RootPath = rootPath }, cancellationToken)
            .ContinueWith(t => t.Result.RootNode, TaskScheduler.Default);
    }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var rootPath = options.RootPath?.TrimEnd('\\');
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return ScanResult.CreateFailed(string.Empty, "Invalid root path specified.", Guid.NewGuid());
        }

        // Normalize root for root drives (e.g. "C:" -> "C:\")
        if (rootPath.Length == 2 && rootPath[1] == ':')
        {
            rootPath += "\\";
        }

        if (!Directory.Exists(rootPath) && !File.Exists(rootPath))
        {
            return ScanResult.CreateFailed(rootPath, $"Target path does not exist or is inaccessible: {rootPath}", Guid.NewGuid());
        }

        CancellationTokenSource linkedCts;
        Guid scanId = Guid.NewGuid();

        lock (_lock)
        {
            _activeCts?.Cancel();
            _activeCts?.Dispose();

            _activeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts = _activeCts;
            _activeScanId = scanId;
        }

        var token = linkedCts.Token;
        var stopwatch = Stopwatch.StartNew();

        Progress.Reset();
        Progress.Status = ScanStatus.Preparing;
        Progress.CurrentPath = rootPath;

        var warnings = new List<ScanWarning>();
        var largestFiles = new List<FileSystemNode>();

        long totalFiles = 0;
        long totalDirs = 0;
        long totalBytes = 0;

        FileSystemNode? rootNode = null;
        ScanStatus finalStatus = ScanStatus.Completed;
        string? errorMessage = null;

        long lastThrottleTimeMs = 0;
        const long ThrottleIntervalMs = 60; // 60ms throttle for UI updates (~16 fps UI sync)

        void ReportProgressThrottled(string currentPath, bool force = false)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            if (force || (elapsedMs - lastThrottleTimeMs >= ThrottleIntervalMs))
            {
                lastThrottleTimeMs = elapsedMs;
                Progress.CurrentPath = currentPath;
                Progress.FilesProcessed = Volatile.Read(ref totalFiles);
                Progress.DirectoriesProcessed = Volatile.Read(ref totalDirs);
                Progress.TotalBytesScanned = Volatile.Read(ref totalBytes);
                Progress.SkippedItemsCount = warnings.Count;
                Progress.ElapsedTime = stopwatch.Elapsed;
            }
        }

        try
        {
            Progress.Status = ScanStatus.Scanning;

            rootNode = await Task.Run(() =>
            {
                return ScanDirectoryRecursive(
                    rootPath,
                    null,
                    0,
                    options,
                    token,
                    warnings,
                    largestFiles,
                    ref totalFiles,
                    ref totalDirs,
                    ref totalBytes,
                    ReportProgressThrottled);
            }, token);

            Progress.Status = ScanStatus.Finalizing;

            if (rootNode != null)
            {
                // Calculate percentage of root across the entire tree
                long rootSize = rootNode.SizeBytes;
                CalculateRootPercentagesRecursive(rootNode, rootSize);
            }

            finalStatus = warnings.Count > 0 ? ScanStatus.CompletedWithWarnings : ScanStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            finalStatus = ScanStatus.Cancelled;
        }
        catch (Exception ex)
        {
            finalStatus = ScanStatus.Failed;
            errorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
        }

        // Ensure progress reflects final snapshot
        ReportProgressThrottled(rootPath, force: true);
        Progress.Status = finalStatus;
        Progress.ElapsedTime = stopwatch.Elapsed;
        Progress.CurrentPath = string.Empty;

        var sortedLargest = largestFiles
            .OrderByDescending(f => f.SizeBytes)
            .Take(options.MaxLargestFilesCount)
            .ToList();

        var scanResult = new ScanResult
        {
            ScanId = scanId,
            RootPath = rootPath,
            RootNode = rootNode,
            Status = finalStatus,
            TotalBytesScanned = rootNode?.SizeBytes ?? totalBytes,
            TotalFilesCount = rootNode?.TotalFileCount ?? totalFiles,
            TotalDirectoriesCount = rootNode?.TotalSubfolderCount ?? totalDirs,
            SkippedCount = warnings.Count,
            Warnings = warnings,
            LargestFiles = sortedLargest,
            ElapsedTime = stopwatch.Elapsed,
            ErrorMessage = errorMessage
        };

        // Guard against race conditions: only update LastResult if this is still the active scan
        lock (_lock)
        {
            if (_activeScanId == scanId)
            {
                LastResult = scanResult;
            }
        }

        ScanCompleted?.Invoke(this, scanResult);
        return scanResult;
    }

    public void CancelScan()
    {
        lock (_lock)
        {
            _activeCts?.Cancel();
        }
    }

    private FileSystemNode ScanDirectoryRecursive(
        string path,
        FileSystemNode? parent,
        int currentDepth,
        ScanOptions options,
        CancellationToken token,
        List<ScanWarning> warnings,
        List<FileSystemNode> largestFiles,
        ref long totalFiles,
        ref long totalDirs,
        ref long totalBytes,
        Action<string, bool> reportProgress)
    {
        token.ThrowIfCancellationRequested();

        var dirInfo = new DirectoryInfo(path);
        bool isReparsePoint = (dirInfo.Attributes & FileAttributes.ReparsePoint) != 0;

        var node = new FileSystemNode
        {
            Name = string.IsNullOrEmpty(dirInfo.Name) ? dirInfo.FullName : dirInfo.Name,
            FullPath = dirInfo.FullName,
            IsDirectory = true,
            Parent = parent,
            Attributes = dirInfo.Attributes,
            IsReparsePoint = isReparsePoint,
            LastModified = SafeGetLastWriteTime(dirInfo)
        };

        Interlocked.Increment(ref totalDirs);
        reportProgress(path, false);

        // Reparse Point / Junction Cycle Prevention:
        // If this directory is a reparse point (junction/symlink) and we are not following them, skip recursion!
        if (isReparsePoint && !options.FollowReparsePoints && parent != null)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.ReparsePointSkipped,
                Message = "Directory junction or symbolic link skipped to prevent recursive loops and double-counting."
            });
            return node;
        }

        // If max depth reached, stop descending
        if (options.MaxDepth.HasValue && currentDepth >= options.MaxDepth.Value)
        {
            return node;
        }

        long nodeTotalSize = 0;
        int directFiles = 0;
        int directSubdirs = 0;
        long totalDescendantFiles = 0;
        long totalDescendantSubdirs = 0;

        // 1. Enumerate Files in current directory
        try
        {
            var files = dirInfo.EnumerateFiles();
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();

                long fileSize = 0;
                DateTime modified = DateTime.MinValue;
                FileAttributes attrs = FileAttributes.Normal;

                try
                {
                    fileSize = file.Length;
                    modified = file.LastWriteTime;
                    attrs = file.Attributes;
                }
                catch (FileNotFoundException)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = file.FullName,
                        WarningType = ScanWarningType.FileNotFound,
                        Message = "File was deleted or relocated during scan."
                    });
                    continue;
                }
                catch (UnauthorizedAccessException ex)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = file.FullName,
                        WarningType = ScanWarningType.UnauthorizedAccess,
                        Message = ex.Message
                    });
                    continue;
                }
                catch (IOException ex)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = file.FullName,
                        WarningType = ScanWarningType.LockedFile,
                        Message = ex.Message
                    });
                    continue;
                }
                catch (Exception ex)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = file.FullName,
                        WarningType = ScanWarningType.GeneralIoError,
                        Message = ex.Message
                    });
                    continue;
                }

                directFiles++;
                Interlocked.Increment(ref totalFiles);
                Interlocked.Add(ref totalBytes, fileSize);
                nodeTotalSize += fileSize;

                var fileNode = new FileSystemNode
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    SizeBytes = fileSize,
                    LastModified = modified,
                    Extension = file.Extension,
                    Attributes = attrs,
                    Parent = node
                };

                // Add to node's direct children
                node.Children.Add(fileNode);

                // Track notable files for Largest Files view
                if (fileSize > 1024 * 1024 || largestFiles.Count < options.MaxLargestFilesCount)
                {
                    lock (largestFiles)
                    {
                        largestFiles.Add(fileNode);
                    }
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.UnauthorizedAccess,
                Message = $"Access denied enumerating files: {ex.Message}"
            });
        }
        catch (DirectoryNotFoundException)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.DirectoryNotFound,
                Message = "Directory was removed during scan."
            });
            return node;
        }
        catch (PathTooLongException)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.PathTooLong,
                Message = "Path exceeds the system-defined maximum length."
            });
        }
        catch (IOException ex)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.GeneralIoError,
                Message = $"I/O error reading directory files: {ex.Message}"
            });
        }

        // 2. Enumerate Subdirectories
        try
        {
            var directories = dirInfo.EnumerateDirectories();
            foreach (var subDir in directories)
            {
                token.ThrowIfCancellationRequested();
                directSubdirs++;

                try
                {
                    var childNode = ScanDirectoryRecursive(
                        subDir.FullName,
                        node,
                        currentDepth + 1,
                        options,
                        token,
                        warnings,
                        largestFiles,
                        ref totalFiles,
                        ref totalDirs,
                        ref totalBytes,
                        reportProgress);

                    node.Children.Add(childNode);
                    nodeTotalSize += childNode.SizeBytes;
                    totalDescendantFiles += childNode.TotalFileCount;
                    totalDescendantSubdirs += childNode.TotalSubfolderCount + 1;
                }
                catch (UnauthorizedAccessException ex)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = subDir.FullName,
                        WarningType = ScanWarningType.UnauthorizedAccess,
                        Message = $"Access denied traversing directory: {ex.Message}"
                    });

                    // Add an inaccessible placeholder node so it is visible in the tree/table
                    var placeholder = new FileSystemNode
                    {
                        Name = subDir.Name,
                        FullPath = subDir.FullName,
                        IsDirectory = true,
                        Parent = node,
                        LastModified = SafeGetLastWriteTime(subDir)
                    };
                    node.Children.Add(placeholder);
                    totalDescendantSubdirs++;
                }
                catch (DirectoryNotFoundException)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = subDir.FullName,
                        WarningType = ScanWarningType.DirectoryNotFound,
                        Message = "Subdirectory was removed before traversal."
                    });
                }
                catch (Exception ex)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = subDir.FullName,
                        WarningType = ScanWarningType.GeneralIoError,
                        Message = ex.Message
                    });
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.UnauthorizedAccess,
                Message = $"Access denied enumerating subdirectories: {ex.Message}"
            });
        }
        catch (DirectoryNotFoundException)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.DirectoryNotFound,
                Message = "Directory was removed during traversal."
            });
        }
        catch (Exception ex)
        {
            warnings.Add(new ScanWarning
            {
                Path = path,
                WarningType = ScanWarningType.GeneralIoError,
                Message = ex.Message
            });
        }

        node.SizeBytes = nodeTotalSize;
        node.DirectFileCount = directFiles;
        node.DirectSubfolderCount = directSubdirs;
        node.TotalFileCount = totalDescendantFiles + directFiles;
        node.TotalSubfolderCount = totalDescendantSubdirs;

        // Sort children initially by size descending and assign PercentOfParent
        var sorted = node.Children.OrderByDescending(c => c.SizeBytes).ToList();
        node.Children.Clear();
        foreach (var child in sorted)
        {
            child.PercentOfParent = nodeTotalSize > 0
                ? ((double)child.SizeBytes / nodeTotalSize) * 100.0
                : 0.0;
            node.Children.Add(child);
        }

        return node;
    }

    private static void CalculateRootPercentagesRecursive(FileSystemNode node, long rootSize)
    {
        node.PercentOfRoot = rootSize > 0
            ? ((double)node.SizeBytes / rootSize) * 100.0
            : 0.0;

        foreach (var child in node.Children)
        {
            CalculateRootPercentagesRecursive(child, rootSize);
        }
    }

    private static DateTime SafeGetLastWriteTime(FileSystemInfo fsi)
    {
        try { return fsi.LastWriteTime; }
        catch { return DateTime.MinValue; }
    }
}

