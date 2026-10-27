using System;
using System.Threading;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Service interface for high-performance file-system scanning and disk space analysis.
/// </summary>
public interface IScanService
{
    bool IsScanning { get; }
    ScanProgressInfo Progress { get; }
    ScanResult? LastResult { get; }

    event EventHandler<ScanResult>? ScanCompleted;

    /// <summary>
    /// Performs an asynchronous scan using customized ScanOptions.
    /// </summary>
    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an asynchronous scan on a root path with default options.
    /// </summary>
    Task<FileSystemNode?> ScanPathAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any currently executing scan operation.
    /// </summary>
    void CancelScan();
}

