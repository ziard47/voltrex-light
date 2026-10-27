using System.Collections.Generic;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Service interface for managing local, versioned scan history.
/// </summary>
public interface IScanHistoryService
{
    IReadOnlyList<ScanHistoryItem> History { get; }
    Task LoadHistoryAsync();
    Task RecordScanAsync(ScanResult result);
    Task ClearHistoryAsync();
}

