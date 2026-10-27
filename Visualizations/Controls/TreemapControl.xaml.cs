using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using voltrex_light.Models;
using voltrex_light.Visualizations.Engines;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Visualizations.Controls;

public sealed partial class TreemapControl : UserControl
{
    private readonly ITreemapLayoutEngine _engine = new SquarifiedTreemapEngine();
    private IReadOnlyList<TreemapItem> _laidOutItems = Array.Empty<TreemapItem>();
    private readonly Dictionary<TreemapItem, Border> _elementMap = new();

    private FileSystemNode? _rootNode;
    private TreemapItem? _hoveredItem;
    private TreemapItem? _selectedItem;
    private bool _showLabels = true;
    private int _maxDepth = 2;

    public event EventHandler<FileSystemNode>? ItemSelected;
    public event EventHandler<FileSystemNode>? DrillDownRequested;

    public FileSystemNode? RootNode
    {
        get => _rootNode;
        set
        {
            if (_rootNode != value)
            {
                _rootNode = value;
                _selectedItem = null;
                _hoveredItem = null;
                HideTooltip();
                RenderTreemap();
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
                RenderTreemap();
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
                RenderTreemap();
            }
        }
    }

    public TreemapControl()
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

        foreach (var item in _laidOutItems)
        {
            if (item.Node == node || item.Node.FullPath == node.FullPath)
            {
                SetSelectedItem(item);
                return;
            }
        }
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 10 && e.NewSize.Height > 10)
        {
            RenderTreemap();
        }
    }

    public void RenderTreemap()
    {
        DrawingCanvas.Children.Clear();
        _elementMap.Clear();

        double w = LayoutRoot.ActualWidth;
        double h = LayoutRoot.ActualHeight;

        if (_rootNode == null || w <= 20 || h <= 20 || _rootNode.SizeBytes <= 0)
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            _laidOutItems = Array.Empty<TreemapItem>();
            return;
        }

        EmptyStatePanel.Visibility = Visibility.Collapsed;

        _laidOutItems = _engine.ComputeLayout(_rootNode, w, h, _maxDepth);

        foreach (var item in _laidOutItems)
        {
            var border = CreateItemElement(item);
            _elementMap[item] = border;

            Canvas.SetLeft(border, item.X);
            Canvas.SetTop(border, item.Y);
            DrawingCanvas.Children.Add(border);
        }
    }

    private Border CreateItemElement(TreemapItem item)
    {
        var border = new Border
        {
            Width = item.Width,
            Height = item.Height,
            Background = new SolidColorBrush(HexToColor(item.ColorHex)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, item.Width, item.Height) }
        };

        if (_showLabels && item.CanRenderLabel)
        {
            var textBlock = new TextBlock
            {
                Text = item.Node.Name,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(4, 2, 4, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            border.Child = textBlock;
        }

        return border;
    }

    private void DrawingCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(DrawingCanvas).Position;
        var hit = _engine.HitTest(_laidOutItems, pos.X, pos.Y);

        if (hit != _hoveredItem)
        {
            // Reset old hover
            if (_hoveredItem != null && _elementMap.TryGetValue(_hoveredItem, out var oldElem) && _hoveredItem != _selectedItem)
            {
                oldElem.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                oldElem.BorderThickness = new Thickness(1);
            }

            _hoveredItem = hit;

            // Highlight new hover
            if (_hoveredItem != null && _elementMap.TryGetValue(_hoveredItem, out var newElem) && _hoveredItem != _selectedItem)
            {
                newElem.BorderBrush = new SolidColorBrush(Colors.White);
                newElem.BorderThickness = new Thickness(2);
            }
        }

        if (_hoveredItem != null)
        {
            ShowTooltip(_hoveredItem, pos.X, pos.Y);
        }
        else
        {
            HideTooltip();
        }
    }

    private void DrawingCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_hoveredItem != null && _elementMap.TryGetValue(_hoveredItem, out var elem) && _hoveredItem != _selectedItem)
        {
            elem.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            elem.BorderThickness = new Thickness(1);
        }

        _hoveredItem = null;
        HideTooltip();
    }

    private void DrawingCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(DrawingCanvas).Position;
        var hit = _engine.HitTest(_laidOutItems, pos.X, pos.Y);

        if (hit != null)
        {
            SetSelectedItem(hit);
            ItemSelected?.Invoke(this, hit.Node);
        }
    }

    private void DrawingCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var pos = e.GetPosition(DrawingCanvas);
        var hit = _engine.HitTest(_laidOutItems, pos.X, pos.Y);

        if (hit != null && hit.Node.IsDirectory && hit.Node.Children.Count > 0)
        {
            DrillDownRequested?.Invoke(this, hit.Node);
        }
    }

    private void SetSelectedItem(TreemapItem item)
    {
        ClearSelection();
        _selectedItem = item;

        if (_elementMap.TryGetValue(item, out var elem))
        {
            elem.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); // Gold highlight
            elem.BorderThickness = new Thickness(2.5);
        }
    }

    private void ClearSelection()
    {
        if (_selectedItem != null && _elementMap.TryGetValue(_selectedItem, out var oldElem))
        {
            oldElem.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            oldElem.BorderThickness = new Thickness(1);
        }
        _selectedItem = null;
    }

    private void ShowTooltip(TreemapItem item, double x, double y)
    {
        TooltipTitle.Text = item.Node.Name;
        TooltipPath.Text = item.Node.FullPath;
        TooltipSize.Text = item.Node.FormattedSize;
        TooltipPercent.Text = $"{item.Node.FormattedPercent} of parent ({item.Node.FormattedPercentOfRoot} of root)";
        TooltipBadge.Background = new SolidColorBrush(HexToColor(item.ColorHex));

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

