using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using voltrex_light.Models;
using voltrex_light.Visualizations.Engines;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Visualizations.Controls;

public sealed partial class SunburstControl : UserControl
{
    private readonly ISunburstLayoutEngine _engine = new SunburstLayoutEngine();
    private IReadOnlyList<SunburstSlice> _laidOutSlices = Array.Empty<SunburstSlice>();
    private readonly Dictionary<SunburstSlice, Path> _elementMap = new();

    private FileSystemNode? _rootNode;
    private SunburstSlice? _hoveredSlice;
    private SunburstSlice? _selectedSlice;
    private bool _showLabels = true;
    private int _maxDepth = 4;
    private double _centerX;
    private double _centerY;

    public event EventHandler<FileSystemNode>? ItemSelected;
    public event EventHandler<FileSystemNode>? DrillDownRequested;
    public event EventHandler? NavigateUpRequested;

    public FileSystemNode? RootNode
    {
        get => _rootNode;
        set
        {
            if (_rootNode != value)
            {
                _rootNode = value;
                _selectedSlice = null;
                _hoveredSlice = null;
                HideTooltip();
                RenderSunburst();
            }
        }
    }

    public bool ShowLabels
    {
        get => _showLabels;
        set
        {
            if (_showLabels != value)
            {
                _showLabels = value;
                RenderSunburst();
            }
        }
    }

    public int MaxDepth
    {
        get => _maxDepth;
        set
        {
            if (_maxDepth != value)
            {
                _maxDepth = value;
                RenderSunburst();
            }
        }
    }

    public SunburstControl()
    {
        InitializeComponent();
    }

    public void SelectNode(FileSystemNode? node)
    {
        if (node == null)
        {
            ClearSelection();
            return;
        }

        foreach (var slice in _laidOutSlices)
        {
            if (slice.Node == node || slice.Node.FullPath == node.FullPath)
            {
                SetSelectedSlice(slice);
                return;
            }
        }
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 10 && e.NewSize.Height > 10)
        {
            RenderSunburst();
        }
    }

    public void RenderSunburst()
    {
        DrawingCanvas.Children.Clear();
        _elementMap.Clear();

        double w = LayoutRoot.ActualWidth;
        double h = LayoutRoot.ActualHeight;

        if (_rootNode == null || w <= 20 || h <= 20 || _rootNode.SizeBytes <= 0)
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            _laidOutSlices = Array.Empty<SunburstSlice>();
            return;
        }

        EmptyStatePanel.Visibility = Visibility.Collapsed;

        _centerX = w / 2.0;
        _centerY = h / 2.0;

        _laidOutSlices = _engine.ComputeLayout(_rootNode, w, h, _maxDepth);

        foreach (var slice in _laidOutSlices)
        {
            var path = CreateSliceElement(slice, _centerX, _centerY);
            _elementMap[slice] = path;
            DrawingCanvas.Children.Add(path);

            if (_showLabels && slice.CanRenderLabel && slice.Depth > 0)
            {
                var label = CreateLabelElement(slice, _centerX, _centerY);
                if (label != null)
                {
                    DrawingCanvas.Children.Add(label);
                }
            }
        }
    }

    private Path CreateSliceElement(SunburstSlice slice, double cx, double cy)
    {
        var geom = new PathGeometry();
        var fig = new PathFigure();

        if (slice.Depth == 0 || slice.InnerRadius <= 0)
        {
            // Center circle
            fig.StartPoint = new Point(cx, cy - slice.OuterRadius);
            fig.Segments.Add(new ArcSegment
            {
                Point = new Point(cx, cy + slice.OuterRadius),
                Size = new Size(slice.OuterRadius, slice.OuterRadius),
                SweepDirection = SweepDirection.Clockwise
            });
            fig.Segments.Add(new ArcSegment
            {
                Point = new Point(cx, cy - slice.OuterRadius),
                Size = new Size(slice.OuterRadius, slice.OuterRadius),
                SweepDirection = SweepDirection.Clockwise
            });
            fig.IsClosed = true;
        }
        else
        {
            // Annular Sector
            double cos1 = Math.Cos(slice.StartAngle);
            double sin1 = Math.Sin(slice.StartAngle);
            double cos2 = Math.Cos(slice.EndAngle);
            double sin2 = Math.Sin(slice.EndAngle);

            Point pInnerStart = new(cx + slice.InnerRadius * cos1, cy + slice.InnerRadius * sin1);
            Point pOuterStart = new(cx + slice.OuterRadius * cos1, cy + slice.OuterRadius * sin1);
            Point pOuterEnd = new(cx + slice.OuterRadius * cos2, cy + slice.OuterRadius * sin2);
            Point pInnerEnd = new(cx + slice.InnerRadius * cos2, cy + slice.InnerRadius * sin2);

            bool isLargeArc = slice.SweepAngle > Math.PI;

            fig.StartPoint = pInnerStart;
            fig.Segments.Add(new LineSegment { Point = pOuterStart });
            fig.Segments.Add(new ArcSegment
            {
                Point = pOuterEnd,
                Size = new Size(slice.OuterRadius, slice.OuterRadius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Clockwise
            });
            fig.Segments.Add(new LineSegment { Point = pInnerEnd });
            fig.Segments.Add(new ArcSegment
            {
                Point = pInnerStart,
                Size = new Size(slice.InnerRadius, slice.InnerRadius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Counterclockwise
            });
            fig.IsClosed = true;
        }

        geom.Figures.Add(fig);

        var path = new Path
        {
            Data = geom,
            Fill = new SolidColorBrush(HexToColor(slice.ColorHex)),
            Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            StrokeThickness = 1.0
        };

        return path;
    }

    private TextBlock? CreateLabelElement(SunburstSlice slice, double cx, double cy)
    {
        double midRadius = (slice.InnerRadius + slice.OuterRadius) / 2.0;
        double midAngle = (slice.StartAngle + slice.EndAngle) / 2.0;
        double arcLength = midRadius * slice.SweepAngle;
        double radialThickness = slice.OuterRadius - slice.InnerRadius;

        if (arcLength < 44 || radialThickness < 18) return null;

        double lx = cx + midRadius * Math.Cos(midAngle);
        double ly = cy + midRadius * Math.Sin(midAngle);

        var tb = new TextBlock
        {
            Text = slice.Node.Name,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 9.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = Math.Min(arcLength * 0.85, 90),
            IsHitTestVisible = false
        };

        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, lx - (tb.DesiredSize.Width / 2.0));
        Canvas.SetTop(tb, ly - (tb.DesiredSize.Height / 2.0));
        return tb;
    }

    private void DrawingCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(DrawingCanvas).Position;
        var hit = _engine.HitTest(_laidOutSlices, pos.X, pos.Y, _centerX, _centerY);

        if (hit != _hoveredSlice)
        {
            if (_hoveredSlice != null && _elementMap.TryGetValue(_hoveredSlice, out var oldPath) && _hoveredSlice != _selectedSlice)
            {
                oldPath.Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                oldPath.StrokeThickness = 1.0;
            }

            _hoveredSlice = hit;

            if (_hoveredSlice != null && _elementMap.TryGetValue(_hoveredSlice, out var newPath) && _hoveredSlice != _selectedSlice)
            {
                newPath.Stroke = new SolidColorBrush(Colors.White);
                newPath.StrokeThickness = 2.0;
            }
        }

        if (_hoveredSlice != null)
        {
            ShowTooltip(_hoveredSlice, pos.X, pos.Y);
        }
        else
        {
            HideTooltip();
        }
    }

    private void DrawingCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_hoveredSlice != null && _elementMap.TryGetValue(_hoveredSlice, out var path) && _hoveredSlice != _selectedSlice)
        {
            path.Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            path.StrokeThickness = 1.0;
        }

        _hoveredSlice = null;
        HideTooltip();
    }

    private void DrawingCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(DrawingCanvas).Position;
        var hit = _engine.HitTest(_laidOutSlices, pos.X, pos.Y, _centerX, _centerY);

        if (hit != null)
        {
            SetSelectedSlice(hit);
            ItemSelected?.Invoke(this, hit.Node);
        }
    }

    private void DrawingCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var pos = e.GetPosition(DrawingCanvas);
        var hit = _engine.HitTest(_laidOutSlices, pos.X, pos.Y, _centerX, _centerY);

        if (hit != null)
        {
            if (hit.Depth == 0)
            {
                // Double tap on center root requests navigating to parent
                NavigateUpRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (hit.Node.IsDirectory && hit.Node.Children.Count > 0)
            {
                DrillDownRequested?.Invoke(this, hit.Node);
            }
        }
    }

    private void SetSelectedSlice(SunburstSlice slice)
    {
        ClearSelection();
        _selectedSlice = slice;

        if (_elementMap.TryGetValue(slice, out var path))
        {
            path.Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); // Gold selection
            path.StrokeThickness = 2.8;
        }
    }

    private void ClearSelection()
    {
        if (_selectedSlice != null && _elementMap.TryGetValue(_selectedSlice, out var oldPath))
        {
            oldPath.Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            oldPath.StrokeThickness = 1.0;
        }
        _selectedSlice = null;
    }

    private void ShowTooltip(SunburstSlice slice, double x, double y)
    {
        TooltipTitle.Text = slice.Node.Name;
        TooltipPath.Text = slice.Node.FullPath;
        TooltipSize.Text = slice.Node.FormattedSize;
        TooltipPercent.Text = $"{slice.Node.FormattedPercent} of parent ({slice.Node.FormattedPercentOfRoot} of root)";
        TooltipBadge.Background = new SolidColorBrush(HexToColor(slice.ColorHex));

        double tooltipX = x + 14;
        double tooltipY = y + 14;

        if (tooltipX + 290 > LayoutRoot.ActualWidth)
        {
            tooltipX = x - 290;
        }

        if (tooltipY + 80 > LayoutRoot.ActualHeight)
        {
            tooltipY = y - 80;
        }

        Canvas.SetLeft(HoverTooltip, Math.Max(4, tooltipX));
        Canvas.SetTop(HoverTooltip, Math.Max(4, tooltipY));
        HoverTooltip.Visibility = Visibility.Visible;
    }

    private void HideTooltip()
    {
        HoverTooltip.Visibility = Visibility.Collapsed;
    }

    private static Color HexToColor(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6) return Colors.DodgerBlue;

        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);

        return Color.FromArgb(255, r, g, b);
    }
}

