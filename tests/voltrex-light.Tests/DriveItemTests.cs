using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Models;

namespace voltrex_light.Tests;

[TestClass]
public class DriveItemTests
{
    [TestMethod]
    public void DisplayName_WithVolumeLabel_IncludesBoth()
    {
        var drive = new DriveItem
        {
            Name = @"C:\",
            VolumeLabel = "OSDisk"
        };

        Assert.AreEqual("OSDisk (C:)", drive.DisplayName);
    }

    [TestMethod]
    public void DisplayName_WithoutVolumeLabel_DefaultsToLocalDisk()
    {
        var drive = new DriveItem
        {
            Name = @"D:\",
            VolumeLabel = ""
        };

        Assert.AreEqual("Local Disk (D:)", drive.DisplayName);
    }

    [TestMethod]
    public void FormattedMetrics_WithValidCapacity_ComputesCorrectly()
    {
        var drive = new DriveItem
        {
            TotalSizeBytes = 100L * 1024 * 1024 * 1024,
            UsedSizeBytes = 40L * 1024 * 1024 * 1024,
            FreeSizeBytes = 60L * 1024 * 1024 * 1024,
            PercentUsed = 40.0
        };

        Assert.AreEqual("100.00 GB", drive.FormattedTotalSize);
        Assert.AreEqual("40.00 GB", drive.FormattedUsedSize);
        Assert.AreEqual("60.00 GB", drive.FormattedFreeSize);
        Assert.AreEqual("40.0%", drive.FormattedPercentUsed);
    }

    [TestMethod]
    public void IconGlyph_DifferentDriveTypes_ReturnsDistinctGlyphs()
    {
        var fixedDrive = new DriveItem { DriveType = DriveType.Fixed };
        var removableDrive = new DriveItem { DriveType = DriveType.Removable };
        var networkDrive = new DriveItem { DriveType = DriveType.Network };

        Assert.IsFalse(string.IsNullOrEmpty(fixedDrive.IconGlyph));
        Assert.IsFalse(string.IsNullOrEmpty(removableDrive.IconGlyph));
        Assert.IsFalse(string.IsNullOrEmpty(networkDrive.IconGlyph));
    }
}

