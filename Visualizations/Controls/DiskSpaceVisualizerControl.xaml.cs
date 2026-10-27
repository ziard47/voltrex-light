using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using voltrex_light.Models;
using voltrex_light.Visualizations.Models;

namespace voltrex_light.Visualizations.Controls;

public sealed partial class DiskSpaceVisualizerControl : UserControl
{
    private FileSystemNode? _dataRoot;
    private FileSystemNode? _currentRoot;
    public ObservableCollection<VisualizationBreadcrumbItem> Breadcrumbs { get; } = new();

    public event EventHandler<FileSystemNode>? ItemSelected;
    public event EventHandler<FileSystemNode>? ItemDrilled;

    public FileSystemNode? DataRoot
    {
        get => _dataRoot;
        set
        {
            if (_dataRoot != value)
            {
                _dataRoot = value;
                if (_currentRoot == null)
                {
                    SetVisualizationRoot(value);
                }
                else
                {
                    UpdateBreadcrumbs();
                    UpdateButtonStates();
                }
            }
        }
    }

    public FileSystemNode? CurrentVisualizationRoot => _currentRoot;

    public DiskSpaceVisualizerControl()
    {
        InitializeComponent();
        BreadcrumbsList.ItemsSource = Breadcrumbs;
    }

    public void SetVisualizationRoot(FileSystemNode? node)
    {
        _currentRoot = node;
        UpdateBreadcrumbs();

        SunburstView.RootNode = _currentRoot;
        TreemapView.RootNode = _currentRoot;

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool canGoUp = _currentRoot != null && (_currentRoot.Parent != null || (_dataRoot != null && _currentRoot != _dataRoot));
        UpButton.IsEnabled = canGoUp;

        bool canGoRoot = _currentRoot != null && _dataRoot != null && _currentRoot != _dataRoot;
        RootButton.IsEnabled = canGoRoot;
    }

    public void SelectNode(FileSystemNode? node)
    {
        SunburstView.SelectNode(node);
        TreemapView.SelectNode(node);
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        if (_currentRoot == null) return;

        var stack = new List<FileSystemNode>();
        var curr = _currentRoot;

        while (curr != null)
        {
            stack.Add(curr);
            if (_dataRoot != null && curr == _dataRoot) break;
            curr = curr.Parent;
        }

        stack.Reverse();

        for (int i = 0; i < stack.Count; i++)
        {
            var item = stack[i];
            Breadcrumbs.Add(new VisualizationBreadcrumbItem
            {
                Name = string.IsNullOrEmpty(item.Name) ? item.FullPath : item.Name,
                FullPath = item.FullPath,
                Node = item,
                IsFirst = (i == 0),
                IsCurrent = (i == stack.Count - 1)
            });
        }
    }

    private void OnVisualizationItemSelected(object? sender, FileSystemNode node)
    {
        ItemSelected?.Invoke(this, node);
    }

    private void OnVisualizationDrillDown(object? sender, FileSystemNode node)
    {
        if (node.IsDirectory)
        {
            SetVisualizationRoot(node);
            ItemSelected?.Invoke(this, node);
            ItemDrilled?.Invoke(this, node);
        }
    }

    private void OnVisualizationNavigateUp(object? sender, EventArgs e)
    {
        NavigateUp();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateUp();
    }

    private void NavigateUp()
    {
        if (_currentRoot != null && _currentRoot.Parent != null)
        {
            var parent = _currentRoot.Parent;
            SetVisualizationRoot(parent);
            ItemSelected?.Invoke(this, parent);
            ItemDrilled?.Invoke(this, parent);
        }
        else if (_currentRoot != null && _dataRoot != null && _currentRoot != _dataRoot)
        {
            SetVisualizationRoot(_dataRoot);
            ItemSelected?.Invoke(this, _dataRoot);
            ItemDrilled?.Invoke(this, _dataRoot);
        }
    }

    private void RootButton_Click(object sender, RoutedEventArgs e)
    {
        if (_dataRoot != null && _currentRoot != _dataRoot)
        {
            SetVisualizationRoot(_dataRoot);
            ItemSelected?.Invoke(this, _dataRoot);
            ItemDrilled?.Invoke(this, _dataRoot);
        }
    }

    private void BreadcrumbButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VisualizationBreadcrumbItem breadcrumb)
        {
            SetVisualizationRoot(breadcrumb.Node);
            ItemSelected?.Invoke(this, breadcrumb.Node);
            ItemDrilled?.Invoke(this, breadcrumb.Node);
        }
    }

    private void ViewMode_Checked(object sender, RoutedEventArgs e)
    {
        if (SunburstView == null || TreemapView == null) return;

        if (SunburstModeRadio?.IsChecked == true)
        {
            SunburstView.Visibility = Visibility.Visible;
            TreemapView.Visibility = Visibility.Collapsed;
            SunburstView.RenderSunburst();
        }
        else if (TreemapModeRadio?.IsChecked == true)
        {
            SunburstView.Visibility = Visibility.Collapsed;
            TreemapView.Visibility = Visibility.Visible;
            TreemapView.RenderTreemap();
        }
    }

    private void LabelsToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = LabelsToggle.IsChecked == true;
        SunburstView.ShowLabels = show;
        TreemapView.ShowLabels = show;
    }

    private void DepthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DepthComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int depth))
        {
            if (SunburstView != null) SunburstView.MaxDepth = depth;
            if (TreemapView != null) TreemapView.MaxDepth = depth;
        }
    }
}

