using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Provides storage drive discovery and Windows disk metrics using System.IO.DriveInfo.
/// </summary>
public class DriveService : IDriveService
{
    public Task<IReadOnlyList<DriveItem>> GetDrivesAsync()
    {
        return Task.Run<IReadOnlyList<DriveItem>>(() =>
        {
            var result = new List<DriveItem>();

            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var d in drives)
                {
                    try
                    {
                        var item = CreateDriveItem(d);
                        result.Add(item);
                    }
                    catch
                    {
                        // Safely handle unreadable / permission denied drives
                        result.Add(new DriveItem
                        {
                            Name = d.Name,
                            RootDirectory = d.RootDirectory.FullName,
                            DriveType = d.DriveType,
                            IsReady = false,
                            VolumeLabel = "Inaccessible"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriveService] Failed to enumerate drives: {ex.Message}");
            }

            return result;
        });
    }

    public Task<DriveItem?> GetDriveAsync(string rootPath)
    {
        return Task.Run<DriveItem?>(() =>
        {
            try
            {
                var driveInfo = new DriveInfo(rootPath);
                return CreateDriveItem(driveInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriveService] Failed to read drive '{rootPath}': {ex.Message}");
                return null;
            }
        });
    }

    private static DriveItem CreateDriveItem(DriveInfo d)
    {
        var item = new DriveItem
        {
            Name = d.Name,
            RootDirectory = d.RootDirectory.FullName,
            DriveType = d.DriveType,
            IsReady = d.IsReady
        };

        if (d.IsReady)
        {
            try
            {
                item.VolumeLabel = d.VolumeLabel;
                item.DriveFormat = d.DriveFormat;
                item.TotalSizeBytes = d.TotalSize;
                item.FreeSizeBytes = d.TotalFreeSpace;
                item.UsedSizeBytes = Math.Max(0, d.TotalSize - d.TotalFreeSpace);
                item.PercentUsed = d.TotalSize > 0
                    ? ((double)item.UsedSizeBytes / d.TotalSize) * 100.0
                    : 0.0;
            }
            catch
            {
                // In case drive was disconnected or queried mid-state
                item.IsReady = false;
            }
        }

        return item;
    }
}

