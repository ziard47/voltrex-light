using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Helpers;

namespace voltrex_light.Tests;

[TestClass]
public class FileSystemFormatterTests
{
    [TestMethod]
    public void FormatBytes_ZeroBytes_ReturnsZeroB()
    {
        string result = FileSystemFormatter.FormatBytes(0);
        Assert.AreEqual("0 B", result);
    }

    [TestMethod]
    public void FormatBytes_SmallBytes_ReturnsExactBytes()
    {
        string result = FileSystemFormatter.FormatBytes(512);
        Assert.AreEqual("512 B", result);
    }

    [TestMethod]
    public void FormatBytes_Kilobytes_FormatsCorrectly()
    {
        string result = FileSystemFormatter.FormatBytes(1024);
        Assert.AreEqual("1.00 KB", result);
    }

    [TestMethod]
    public void FormatBytes_Megabytes_FormatsCorrectly()
    {
        string result = FileSystemFormatter.FormatBytes(5 * 1024 * 1024);
        Assert.AreEqual("5.00 MB", result);
    }

    [TestMethod]
    public void FormatBytes_Gigabytes_FormatsCorrectly()
    {
        string result = FileSystemFormatter.FormatBytes(10L * 1024 * 1024 * 1024);
        Assert.AreEqual("10.00 GB", result);
    }

    [TestMethod]
    public void FormatBytes_NegativeBytes_ReturnsMinusPrefix()
    {
        string result = FileSystemFormatter.FormatBytes(-1024);
        Assert.AreEqual("-1.00 KB", result);
    }

    [TestMethod]
    public void FormatPercentage_ValidRatio_FormatsAccurately()
    {
        string result = FileSystemFormatter.FormatPercentage(0.754);
        Assert.AreEqual("75.4%", result);
    }

    [TestMethod]
    public void FormatPercentage_AbsolutePercentage_FormatsAccurately()
    {
        string result = FileSystemFormatter.FormatPercentage(85.5, isRatio0To1: false);
        Assert.AreEqual("85.5%", result);
    }

    [TestMethod]
    public void FormatBytes_BinaryUnits_FormatsWithIecSuffixes()
    {
        string kb = FileSystemFormatter.FormatBytes(1024, formatOverride: SizeUnitFormat.Binary);
        Assert.AreEqual("1.00 KiB", kb);

        string mb = FileSystemFormatter.FormatBytes(5 * 1024 * 1024, formatOverride: SizeUnitFormat.Binary);
        Assert.AreEqual("5.00 MiB", mb);

        string gb = FileSystemFormatter.FormatBytes(10L * 1024 * 1024 * 1024, formatOverride: SizeUnitFormat.Binary);
        Assert.AreEqual("10.00 GiB", gb);
    }

    [TestMethod]
    public void FormatBytes_ActiveUnitFormatSwitch_AppliesGlobally()
    {
        var prev = FileSystemFormatter.ActiveUnitFormat;
        try
        {
            FileSystemFormatter.ActiveUnitFormat = SizeUnitFormat.Binary;
            Assert.AreEqual("1.00 GiB", FileSystemFormatter.FormatBytes(1024L * 1024 * 1024));

            FileSystemFormatter.ActiveUnitFormat = SizeUnitFormat.Standard;
            Assert.AreEqual("1.00 GB", FileSystemFormatter.FormatBytes(1024L * 1024 * 1024));
        }
        finally
        {
            FileSystemFormatter.ActiveUnitFormat = prev;
        }
    }

    [TestMethod]
    public void FormatCount_PluralAndSingular_FormatsCorrectly()
    {
        Assert.AreEqual("1 file", FileSystemFormatter.FormatCount(1, "file", "files"));
        Assert.AreEqual("42 files", FileSystemFormatter.FormatCount(42, "file", "files"));
        Assert.AreEqual("1,000 files", FileSystemFormatter.FormatCount(1000, "file", "files"));
    }
}

