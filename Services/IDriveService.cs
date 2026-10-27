using System.Collections.Generic;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Service interface for discovering storage drives and retrieving capacity metrics.
/// </summary>
public interface IDriveService
{
    /// <summary>
    /// Enumerates all local and attached drives with real Windows capacity statistics.
    /// </summary>
    Task<IReadOnlyList<DriveItem>> GetDrivesAsync();

    /// <summary>
    /// Gets updated information for a specific drive root path (e.g. "C:\").
    /// </summary>
    Task<DriveItem?> GetDriveAsync(string rootPath);
}

