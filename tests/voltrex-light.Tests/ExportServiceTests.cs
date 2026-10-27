using System;
using System.Collections.Generic;
using System.Text.Json;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.Tests;

[TestClass]
public class ExportServiceTests
{
    private ExportService _exportService = null!;

    [TestInitialize]
    public void Setup()
    {
        _exportService = new ExportService();
    }

    [TestMethod]
    public void ExportToCsv_ThrowsOnNullResult()
    {
        Assert.ThrowsException<ArgumentNullException>(() => _exportService.ExportToCsv(null!));
    }

    [TestMethod]
    public void ExportToCsv_Preserves64BitPrecisionAndEscapesSpecialCharacters()
    {
        var root = new FileSystemNode
        {
            Name = "Root, \"Test\"",
            FullPath = "C:\\Root, \"Test\"",
            IsDirectory = true,
            SizeBytes = 12_884_901_888L, // ~12 GB
            TotalFileCount = 2,
            TotalSubfolderCount = 1
        };

        var childFolder = new FileSystemNode
        {
            Name = "Sub\nFolder",
            FullPath = "C:\\Root, \"Test\"\\Sub\nFolder",
            IsDirectory = true,
            SizeBytes = 8_589_934_592L,
            Parent = root
        };

        var childFile = new FileSystemNode
        {
            Name = "Large, File \"1\".dat",
            FullPath = "C:\\Root, \"Test\"\\Sub\nFolder\\Large, File \"1\".dat",
            IsDirectory = false,
            SizeBytes = 4_294_967_296L, // Exactly 4 GiB
            Extension = ".dat",
            Parent = childFolder
        };

        childFolder.Children.Add(childFile);
        root.Children.Add(childFolder);

        var scanResult = new ScanResult
        {
            RootPath = root.FullPath,
            RootNode = root,
            Status = ScanStatus.Completed,
            TotalBytesScanned = root.SizeBytes,
            TotalFilesCount = 1,
            TotalDirectoriesCount = 1,
            SkippedCount = 0,
            ElapsedTime = TimeSpan.FromSeconds(2.5)
        };

        var csv = _exportService.ExportToCsv(scanResult);

        Assert.IsNotNull(csv);
        Assert.IsTrue(csv.Contains("# DiskScope Scan Report"));
        Assert.IsTrue(csv.Contains("12884901888"), "Logical byte size must not lose precision");
        Assert.IsTrue(csv.Contains("4294967296"), "File logical byte size must match exactly");
        Assert.IsTrue(csv.Contains("\"Root, \"\"Test\"\"\""), "Quotes and commas in root name must be RFC 4180 escaped");
        Assert.IsTrue(csv.Contains("\"Sub\nFolder\""), "Newlines in directory name must be quoted");
        Assert.IsTrue(csv.Contains("\"Large, File \"\"1\"\".dat\""), "Quotes and commas in file name must be quoted and escaped");
    }

    [TestMethod]
    public void ExportToJson_ProducesValidJsonAndConformsToSchema()
    {
        var root = new FileSystemNode
        {
            Name = "ProjectRoot",
            FullPath = "D:\\Projects",
            IsDirectory = true,
            SizeBytes = 5_000_000L,
            TotalFileCount = 3,
            TotalSubfolderCount = 0
        };

        var file1 = new FileSystemNode
        {
            Name = "app.exe",
            FullPath = "D:\\Projects\\app.exe",
            IsDirectory = false,
            SizeBytes = 3_000_000L,
            Extension = ".exe",
            Parent = root
        };

        root.Children.Add(file1);

        var scanResult = new ScanResult
        {
            RootPath = root.FullPath,
            RootNode = root,
            Status = ScanStatus.CompletedWithWarnings,
            TotalBytesScanned = 5_000_000L,
            TotalFilesCount = 3,
            TotalDirectoriesCount = 0,
            SkippedCount = 1,
            Warnings = new List<ScanWarning>
            {
                new()
                {
                    Path = "D:\\Projects\\restricted",
                    WarningType = ScanWarningType.UnauthorizedAccess,
                    Message = "Access is denied."
                }
            },
            ElapsedTime = TimeSpan.FromMilliseconds(450)
        };

        var json = _exportService.ExportToJson(scanResult);

        Assert.IsNotNull(json);

        // Verify valid JSON parseability
        using var doc = JsonDocument.Parse(json);
        var rootElem = doc.RootElement;

        Assert.AreEqual("1.0", rootElem.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("DiskScope", rootElem.GetProperty("application").GetString());

        var metadata = rootElem.GetProperty("scanMetadata");
        Assert.AreEqual("D:\\Projects", metadata.GetProperty("rootPath").GetString());
        Assert.AreEqual("CompletedWithWarnings", metadata.GetProperty("status").GetString());
        Assert.IsTrue(metadata.GetProperty("isPartial").GetBoolean());
        Assert.AreEqual(5000000L, metadata.GetProperty("totalBytesScanned").GetInt64());
        Assert.AreEqual(1, metadata.GetProperty("skippedCount").GetInt32());

        var warnings = metadata.GetProperty("warnings");
        Assert.AreEqual(1, warnings.GetArrayLength());
        Assert.AreEqual("UnauthorizedAccess", warnings[0].GetProperty("warningType").GetString());

        var tree = rootElem.GetProperty("tree");
        Assert.AreEqual("ProjectRoot", tree.GetProperty("name").GetString());
        Assert.IsTrue(tree.GetProperty("isDirectory").GetBoolean());
        Assert.AreEqual(1, tree.GetProperty("children").GetArrayLength());
    }
}

