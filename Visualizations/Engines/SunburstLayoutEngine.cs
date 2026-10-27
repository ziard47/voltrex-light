using System;
using System.Collections.Generic;
using System.Linq;
using voltrex_light.Models;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Visualizations.Engines;

/// <summary>
/// Filelight-inspired radial sunburst layout engine.
/// Computes multi-tier concentric annular sectors preserving strict angular parent-child containment.
/// </summary>
public class SunburstLayoutEngine : ISunburstLayoutEngine
{
    private const double CenterHoleRatio = 0.22;

    public IReadOnlyList<SunburstSlice> ComputeLayout(
        FileSystemNode rootNode,
        double availableWidth,
        double availableHeight,
        int maxDepth = 4,
        double minAngleThreshold = 0.015)
    {
        var result = new List<SunburstSlice>();

        if (rootNode == null || availableWidth <= 20 || availableHeight <= 20)
        {
            return result;
        }

        double maxRadius = Math.Min(availableWidth, availableHeight) / 2.0 - 4.0;
        if (maxRadius <= 15) return result;

        int actualDepth = GetSubtreeMaxDepth(rootNode, 0, maxDepth);
        int effectiveDepth = Math.Max(1, Math.Min(maxDepth, actualDepth));

        double centerRadius = Math.Max(30.0, maxRadius * CenterHoleRatio);
        double ringThickness = (maxRadius - centerRadius) / effectiveDepth;

        // Center disk (Depth 0 - root)
        var centerSlice = new SunburstSlice
        {
            Node = rootNode,
            InnerRadius = 0,
            OuterRadius = centerRadius,
            StartAngle = 0,
            EndAngle = 2.0 * Math.PI,
            Depth = 0,
            ColorHex = "#1E293B" // Deep slate for root center
        };
        result.Add(centerSlice);

        if (rootNode.SizeBytes <= 0 || rootNode.Children.Count == 0)
        {
            return result;
        }

        // Layout concentric rings recursively
        LayoutWedgesRecursive(
            rootNode,
            startAngle: 0.0,
            endAngle: 2.0 * Math.PI,
            currentDepth: 1,
            maxDepth: maxDepth,
            centerRadius: centerRadius,
            ringThickness: ringThickness,
            minAngleThreshold: minAngleThreshold,
            branchIndex: 0,
            result: result);

        return result;
    }

    public SunburstSlice? HitTest(
        IReadOnlyList<SunburstSlice> slices,
        double px,
        double py,
        double centerX,
        double centerY)
    {
        if (slices == null || slices.Count == 0) return null;

        double dx = px - centerX;
        double dy = py - centerY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // Reverse search so outer/deeper rings are hit first
        for (int i = slices.Count - 1; i >= 0; i--)
        {
            var slice = slices[i];
            if (slice.HitTest(px, py, centerX, centerY))
            {
                return slice;
            }
        }

        return null;
    }

    private void LayoutWedgesRecursive(
        FileSystemNode parentNode,
        double startAngle,
        double endAngle,
        int currentDepth,
        int maxDepth,
        double centerRadius,
        double ringThickness,
        double minAngleThreshold,
        int branchIndex,
        List<SunburstSlice> result)
    {
        if (currentDepth > maxDepth) return;

        double availableAngle = endAngle - startAngle;
        if (availableAngle < minAngleThreshold) return;

        var children = parentNode.Children
            .Where(c => c.SizeBytes > 0)
            .OrderByDescending(c => c.SizeBytes)
            .ToList();

        if (children.Count == 0) return;

        long childrenTotalSize = children.Sum(c => c.SizeBytes);
        if (childrenTotalSize <= 0) return;

        double innerRadius = centerRadius + (currentDepth - 1) * ringThickness;
        double outerRadius = innerRadius + ringThickness;

        double currentAngle = startAngle;
        int childBranch = branchIndex;

        foreach (var child in children)
        {
            double sweep = availableAngle * ((double)child.SizeBytes / childrenTotalSize);
            if (sweep <= 0.0001) continue;

            double sliceEnd = Math.Min(endAngle, currentAngle + sweep);

            if (sweep >= minAngleThreshold)
            {
                string color = PaletteProvider.GetColorForNode(childBranch, currentDepth, child.IsDirectory);

                var slice = new SunburstSlice
                {
                    Node = child,
                    InnerRadius = innerRadius,
                    OuterRadius = outerRadius,
                    StartAngle = currentAngle,
                    EndAngle = sliceEnd,
                    Depth = currentDepth,
                    ColorHex = color
                };

                result.Add(slice);

                if (child.IsDirectory && child.Children.Count > 0)
                {
                    LayoutWedgesRecursive(
                        child,
                        currentAngle,
                        sliceEnd,
                        currentDepth + 1,
                        maxDepth,
                        centerRadius,
                        ringThickness,
                        minAngleThreshold,
                        childBranch,
                        result);
                }
            }

            currentAngle = sliceEnd;
            childBranch++;
        }
    }

    private static int GetSubtreeMaxDepth(FileSystemNode node, int currentDepth, int limit)
    {
        if (currentDepth >= limit || node.Children.Count == 0) return currentDepth;

        int deepest = currentDepth;
        foreach (var child in node.Children)
        {
            if (child.IsDirectory && child.Children.Count > 0)
            {
                int d = GetSubtreeMaxDepth(child, currentDepth + 1, limit);
                if (d > deepest) deepest = d;
                if (deepest >= limit) break;
            }
            else
            {
                if (currentDepth + 1 > deepest) deepest = currentDepth + 1;
            }
        }
        return deepest;
    }
}

