using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Models;
using voltrex_light.Visualizations.Engines;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Tests;

[TestClass]
public class VisualizationEngineTests
{
    private FileSystemNode CreateTestTree()
    {
        var root = new FileSystemNode
        {
            Name = "Root",
            FullPath = @"C:\Root",
            IsDirectory = true,
            SizeBytes = 10000
        };

        var childA = new FileSystemNode
        {
            Name = "FolderA",
            FullPath = @"C:\Root\FolderA",
            IsDirectory = true,
            SizeBytes = 6000,
            Parent = root
        };

        var childB = new FileSystemNode
        {
            Name = "FolderB",
            FullPath = @"C:\Root\FolderB",
            IsDirectory = true,
            SizeBytes = 4000,
            Parent = root
        };

        var subA1 = new FileSystemNode
        {
            Name = "SubA1",
            FullPath = @"C:\Root\FolderA\SubA1",
            IsDirectory = false,
            SizeBytes = 4500,
            Parent = childA
        };

        var subA2 = new FileSystemNode
        {
            Name = "SubA2",
            FullPath = @"C:\Root\FolderA\SubA2",
            IsDirectory = false,
            SizeBytes = 1500,
            Parent = childA
        };

        childA.Children.Add(subA1);
        childA.Children.Add(subA2);

        root.Children.Add(childA);
        root.Children.Add(childB);

        return root;
    }

    [TestMethod]
    public void SquarifiedTreemap_AreaProportionality_RectanglesReflectLogicalSizes()
    {
        var engine = new SquarifiedTreemapEngine();
        var root = CreateTestTree();

        double width = 1000;
        double height = 500;
        double totalContainerArea = width * height; // 500,000

        var items = engine.ComputeLayout(root, width, height, maxDepth: 1);

        Assert.IsNotNull(items);
        Assert.AreEqual(2, items.Count);

        var itemA = items.FirstOrDefault(i => i.Node.Name == "FolderA");
        var itemB = items.FirstOrDefault(i => i.Node.Name == "FolderB");

        Assert.IsNotNull(itemA);
        Assert.IsNotNull(itemB);

        // FolderA is 60% of total, FolderB is 40% of total
        double expectedAreaA = totalContainerArea * 0.60;
        double expectedAreaB = totalContainerArea * 0.40;

        Assert.AreEqual(expectedAreaA, itemA.Area, 10.0);
        Assert.AreEqual(expectedAreaB, itemB.Area, 10.0);
    }

    [TestMethod]
    public void SquarifiedTreemap_HitTest_AccuratelyIdentifiesTargetNode()
    {
        var engine = new SquarifiedTreemapEngine();
        var root = CreateTestTree();

        var items = engine.ComputeLayout(root, 800, 600, maxDepth: 1);
        var itemA = items.First(i => i.Node.Name == "FolderA");

        // Center of ItemA
        double testX = itemA.X + itemA.Width / 2.0;
        double testY = itemA.Y + itemA.Height / 2.0;

        var hit = engine.HitTest(items, testX, testY);
        Assert.IsNotNull(hit);
        Assert.AreEqual("FolderA", hit.Node.Name);

        // Outside bounds
        var outsideHit = engine.HitTest(items, -50, -50);
        Assert.IsNull(outsideHit);
    }

    [TestMethod]
    public void SquarifiedTreemap_EmptyOrZeroByteNodes_DoesNotBreakLayoutOrThrow()
    {
        var engine = new SquarifiedTreemapEngine();
        var root = new FileSystemNode
        {
            Name = "EmptyRoot",
            SizeBytes = 0,
            IsDirectory = true
        };

        var emptyChild = new FileSystemNode
        {
            Name = "ZeroFile.txt",
            SizeBytes = 0,
            IsDirectory = false
        };
        root.Children.Add(emptyChild);

        var items = engine.ComputeLayout(root, 600, 400);
        Assert.IsNotNull(items);
        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public void SquarifiedTreemap_SingleChildHierarchy_FillsEntireSpace()
    {
        var engine = new SquarifiedTreemapEngine();
        var root = new FileSystemNode { Name = "Root", SizeBytes = 1000 };
        var singleChild = new FileSystemNode { Name = "OnlyChild", SizeBytes = 1000, Parent = root };
        root.Children.Add(singleChild);

        var items = engine.ComputeLayout(root, 500, 300, maxDepth: 1);
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(500 * 300, items[0].Area, 1.0);
    }

    [TestMethod]
    public void Sunburst_AngularProportionality_SlicesProportionalToSiblings()
    {
        var engine = new SunburstLayoutEngine();
        var root = CreateTestTree();

        var slices = engine.ComputeLayout(root, 600, 600, maxDepth: 1);

        // Depth 0 = Center root slice, Depth 1 = FolderA and FolderB
        Assert.IsNotNull(slices);
        Assert.IsTrue(slices.Count >= 3);

        var sliceRoot = slices.First(s => s.Depth == 0);
        Assert.AreEqual(0, sliceRoot.InnerRadius);
        Assert.AreEqual(2.0 * Math.PI, sliceRoot.SweepAngle, 0.001);

        var sliceA = slices.First(s => s.Node.Name == "FolderA");
        var sliceB = slices.First(s => s.Node.Name == "FolderB");

        // Total angle is 2*PI. FolderA has 6000/10000 = 60%, FolderB has 40%
        double expectedAngleA = 2.0 * Math.PI * 0.60;
        double expectedAngleB = 2.0 * Math.PI * 0.40;

        Assert.AreEqual(expectedAngleA, sliceA.SweepAngle, 0.05);
        Assert.AreEqual(expectedAngleB, sliceB.SweepAngle, 0.05);
    }

    [TestMethod]
    public void Sunburst_ParentChildContainment_ChildrenAreStrictlyInsideParentAngles()
    {
        var engine = new SunburstLayoutEngine();
        var root = CreateTestTree();

        var slices = engine.ComputeLayout(root, 800, 800, maxDepth: 2);

        var sliceA = slices.First(s => s.Node.Name == "FolderA");
        var subA1 = slices.FirstOrDefault(s => s.Node.Name == "SubA1");
        var subA2 = slices.FirstOrDefault(s => s.Node.Name == "SubA2");

        Assert.IsNotNull(subA1);
        Assert.IsNotNull(subA2);

        // SubA1 and SubA2 must be bounded within FolderA's [StartAngle, EndAngle]
        Assert.IsTrue(subA1.StartAngle >= sliceA.StartAngle - 0.001);
        Assert.IsTrue(subA1.EndAngle <= sliceA.EndAngle + 0.001);

        Assert.IsTrue(subA2.StartAngle >= sliceA.StartAngle - 0.001);
        Assert.IsTrue(subA2.EndAngle <= sliceA.EndAngle + 0.001);

        // SubA1 + SubA2 sweeps must sum to parent's sweep
        Assert.AreEqual(sliceA.SweepAngle, subA1.SweepAngle + subA2.SweepAngle, 0.05);
    }

    [TestMethod]
    public void Sunburst_HitTest_DetectsCenterRootAndRadialWedges()
    {
        var engine = new SunburstLayoutEngine();
        var root = CreateTestTree();

        double w = 600;
        double h = 600;
        double cx = 300;
        double cy = 300;

        var slices = engine.ComputeLayout(root, w, h, maxDepth: 1);

        // Center hit
        var centerHit = engine.HitTest(slices, cx, cy, cx, cy);
        Assert.IsNotNull(centerHit);
        Assert.AreEqual(0, centerHit.Depth);
        Assert.AreEqual("Root", centerHit.Node.Name);

        // Out of circle hit
        var outHit = engine.HitTest(slices, 0, 0, cx, cy);
        Assert.IsNull(outHit);
    }

    [TestMethod]
    public void Sunburst_DeepHierarchy_LaysOutMultipleRings()
    {
        var engine = new SunburstLayoutEngine();
        var root = new FileSystemNode { Name = "D0", SizeBytes = 1000, IsDirectory = true };
        var d1 = new FileSystemNode { Name = "D1", SizeBytes = 1000, IsDirectory = true, Parent = root };
        var d2 = new FileSystemNode { Name = "D2", SizeBytes = 1000, IsDirectory = true, Parent = d1 };
        var d3 = new FileSystemNode { Name = "D3", SizeBytes = 1000, IsDirectory = true, Parent = d2 };
        var d4 = new FileSystemNode { Name = "D4", SizeBytes = 1000, IsDirectory = true, Parent = d3 };

        d3.Children.Add(d4);
        d2.Children.Add(d3);
        d1.Children.Add(d2);
        root.Children.Add(d1);

        var slices = engine.ComputeLayout(root, 600, 600, maxDepth: 4);

        Assert.IsTrue(slices.Any(s => s.Depth == 0));
        Assert.IsTrue(slices.Any(s => s.Depth == 1));
        Assert.IsTrue(slices.Any(s => s.Depth == 2));
        Assert.IsTrue(slices.Any(s => s.Depth == 3));
        Assert.IsTrue(slices.Any(s => s.Depth == 4));
    }

    [TestMethod]
    public void Sunburst_VerySmallItems_BelowThresholdAreSkippedToPreventSlivers()
    {
        var engine = new SunburstLayoutEngine();
        var root = new FileSystemNode { Name = "Root", SizeBytes = 1_000_000, IsDirectory = true };
        
        // Massive child taking 99.999%
        var huge = new FileSystemNode { Name = "Huge", SizeBytes = 999_990, IsDirectory = true, Parent = root };
        
        // Microscopic child taking 0.001% (less than minAngleThreshold ~0.015 radians)
        var tiny = new FileSystemNode { Name = "Tiny", SizeBytes = 10, IsDirectory = false, Parent = root };

        root.Children.Add(huge);
        root.Children.Add(tiny);

        var slices = engine.ComputeLayout(root, 600, 600, maxDepth: 1, minAngleThreshold: 0.015);

        // Huge should be present, Tiny should be filtered out from rendering slivers
        Assert.IsTrue(slices.Any(s => s.Node.Name == "Huge"));
        Assert.IsFalse(slices.Any(s => s.Node.Name == "Tiny"));
    }

    [TestMethod]
    public void BreadcrumbTrail_CorrectlyReflectsAncestry()
    {
        var root = new FileSystemNode { Name = "C:", FullPath = @"C:\", IsDirectory = true };
        var users = new FileSystemNode { Name = "Users", FullPath = @"C:\Users", IsDirectory = true, Parent = root };
        var mohom = new FileSystemNode { Name = "mohom", FullPath = @"C:\Users\mohom", IsDirectory = true, Parent = users };

        // Test breadcrumb reconstruction
        var trail = new System.Collections.Generic.List<FileSystemNode>();
        var curr = mohom;
        while (curr != null)
        {
            trail.Add(curr);
            curr = curr.Parent;
        }
        trail.Reverse();

        Assert.AreEqual(3, trail.Count);
        Assert.AreEqual("C:", trail[0].Name);
        Assert.AreEqual("Users", trail[1].Name);
        Assert.AreEqual("mohom", trail[2].Name);
    }
}
