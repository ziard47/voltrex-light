using System;
using System.Collections.Generic;
using System.Linq;
using voltrex_light.Models;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Visualizations.Engines;

/// <summary>
/// Implements the classic Squarified Treemap layout algorithm (Bruls, Huizing, van Wijk).
/// Generates optimal aspect-ratio rectangles with hierarchical drilldown support.
/// </summary>
public class SquarifiedTreemapEngine : ITreemapLayoutEngine
{
    private const double MinRenderDimension = 2.0;

    public IReadOnlyList<TreemapItem> ComputeLayout(
        FileSystemNode rootNode,
        double width,
        double height,
        int maxDepth = 2)
    {
        var result = new List<TreemapItem>();

        if (rootNode == null || width <= 0 || height <= 0)
        {
            return result;
        }

        // Layout the children of the visualization root inside the bounds
        LayoutChildrenRecursive(rootNode, 0, 0, width, height, 1, maxDepth, result, 0);

        return result;
    }

    public TreemapItem? HitTest(IReadOnlyList<TreemapItem> items, double x, double y)
    {
        if (items == null || items.Count == 0) return null;

        // Traverse in reverse order so leaf/deepest items are hit first
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.HitTest(x, y))
            {
                return item;
            }
        }

        return null;
    }

    private void LayoutChildrenRecursive(
        FileSystemNode node,
        double x,
        double y,
        double width,
        double height,
        int currentDepth,
        int maxDepth,
        List<TreemapItem> result,
        int branchIndex)
    {
        if (width < MinRenderDimension || height < MinRenderDimension)
        {
            return;
        }

        var children = node.Children
            .Where(c => c.SizeBytes > 0)
            .OrderByDescending(c => c.SizeBytes)
            .ToList();

        if (children.Count == 0)
        {
            return;
        }

        long totalSize = children.Sum(c => c.SizeBytes);
        if (totalSize <= 0)
        {
            return;
        }

        double totalArea = width * height;
        var normalizedItems = children.Select(c => new LayoutInput
        {
            Node = c,
            Area = (double)c.SizeBytes / totalSize * totalArea
        }).ToList();

        var container = new RectContainer(x, y, width, height);
        var rows = Squarify(normalizedItems, container);

        int childBranch = branchIndex;
        foreach (var item in rows)
        {
            if (item.Width < 1 || item.Height < 1) continue;

            bool isLeaf = currentDepth >= maxDepth || item.Node.Children.Count == 0 || !item.Node.IsDirectory;
            string color = PaletteProvider.GetColorForNode(childBranch++, currentDepth, item.Node.IsDirectory);

            var treemapItem = new TreemapItem
            {
                Node = item.Node,
                X = item.X,
                Y = item.Y,
                Width = item.Width,
                Height = item.Height,
                Depth = currentDepth,
                IsLeaf = isLeaf,
                ColorHex = color
            };

            result.Add(treemapItem);

            // If not leaf and space permits, lay out sub-children inside with padding
            if (!isLeaf && item.Width >= 40 && item.Height >= 40)
            {
                double pad = 2.0;
                double innerX = item.X + pad;
                double innerY = item.Y + pad + (treemapItem.CanRenderLabel ? 18.0 : 0.0);
                double innerW = item.Width - (pad * 2);
                double innerH = item.Height - (innerY - item.Y) - pad;

                if (innerW > MinRenderDimension && innerH > MinRenderDimension)
                {
                    LayoutChildrenRecursive(
                        item.Node,
                        innerX,
                        innerY,
                        innerW,
                        innerH,
                        currentDepth + 1,
                        maxDepth,
                        result,
                        childBranch);
                }
            }
        }
    }

    private List<PlacedItem> Squarify(List<LayoutInput> items, RectContainer container)
    {
        var placed = new List<PlacedItem>();
        SquarifyRow(items, new List<LayoutInput>(), container, placed);
        return placed;
    }

    private void SquarifyRow(
        List<LayoutInput> remaining,
        List<LayoutInput> currentRow,
        RectContainer container,
        List<PlacedItem> placed)
    {
        if (remaining.Count == 0)
        {
            if (currentRow.Count > 0)
            {
                LayoutRow(currentRow, container, placed);
            }
            return;
        }

        var nextItem = remaining[0];
        double currentShortSide = container.ShorterSide;

        if (currentRow.Count == 0)
        {
            currentRow.Add(nextItem);
            remaining.RemoveAt(0);
            SquarifyRow(remaining, currentRow, container, placed);
            return;
        }

        double currentWorst = WorstAspect(currentRow, currentShortSide);
        var testRow = new List<LayoutInput>(currentRow) { nextItem };
        double testWorst = WorstAspect(testRow, currentShortSide);

        if (testWorst <= currentWorst)
        {
            // Adding item improves or maintains aspect ratio
            currentRow.Add(nextItem);
            remaining.RemoveAt(0);
            SquarifyRow(remaining, currentRow, container, placed);
        }
        else
        {
            // Adding item degrades aspect ratio; finalize currentRow and start new row
            var remainingContainer = LayoutRow(currentRow, container, placed);
            currentRow.Clear();
            SquarifyRow(remaining, currentRow, remainingContainer, placed);
        }
    }

    private RectContainer LayoutRow(List<LayoutInput> row, RectContainer container, List<PlacedItem> placed)
    {
        double rowArea = row.Sum(i => i.Area);
        bool isHorizontal = container.Width >= container.Height;

        if (isHorizontal)
        {
            double rowWidth = container.Height > 0 ? rowArea / container.Height : 0;
            double currentY = container.Y;

            foreach (var item in row)
            {
                double itemHeight = rowWidth > 0 ? item.Area / rowWidth : 0;
                placed.Add(new PlacedItem
                {
                    Node = item.Node,
                    X = container.X,
                    Y = currentY,
                    Width = rowWidth,
                    Height = itemHeight
                });
                currentY += itemHeight;
            }

            return new RectContainer(
                container.X + rowWidth,
                container.Y,
                Math.Max(0, container.Width - rowWidth),
                container.Height);
        }
        else
        {
            double rowHeight = container.Width > 0 ? rowArea / container.Width : 0;
            double currentX = container.X;

            foreach (var item in row)
            {
                double itemWidth = rowHeight > 0 ? item.Area / rowHeight : 0;
                placed.Add(new PlacedItem
                {
                    Node = item.Node,
                    X = currentX,
                    Y = container.Y,
                    Width = itemWidth,
                    Height = rowHeight
                });
                currentX += itemWidth;
            }

            return new RectContainer(
                container.X,
                container.Y + rowHeight,
                container.Width,
                Math.Max(0, container.Height - rowHeight));
        }
    }

    private static double WorstAspect(List<LayoutInput> row, double sideLength)
    {
        if (row.Count == 0 || sideLength <= 0) return double.MaxValue;

        double sumArea = row.Sum(i => i.Area);
        if (sumArea <= 0) return double.MaxValue;

        double maxAspect = 0;
        double sideSquared = sideLength * sideLength;

        foreach (var item in row)
        {
            if (item.Area <= 0) continue;
            double r1 = (sideSquared * item.Area) / (sumArea * sumArea);
            double r2 = (sumArea * sumArea) / (sideSquared * item.Area);
            double aspect = Math.Max(r1, r2);
            if (aspect > maxAspect)
            {
                maxAspect = aspect;
            }
        }

        return maxAspect;
    }

    private class LayoutInput
    {
        public FileSystemNode Node { get; init; } = null!;
        public double Area { get; init; }
    }

    private class PlacedItem
    {
        public FileSystemNode Node { get; init; } = null!;
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
    }

    private class RectContainer
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double ShorterSide => Math.Min(Width, Height);

        public RectContainer(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
        }
    }
}

