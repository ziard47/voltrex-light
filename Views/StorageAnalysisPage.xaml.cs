using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using voltrex_light.Helpers;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Views;

public sealed partial class StorageAnalysisPage : Page
{
    public StorageAnalysisViewModel ViewModel => AppServices.StorageAnalysisViewModel;
    private bool _isDetailsPanelOpen = true;
    private bool _isChartOpen = true;

    public StorageAnalysisPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncVisualizerRoot();

        ViewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(StorageAnalysisViewModel.CurrentDirectoryNode) ||
                args.PropertyName == nameof(StorageAnalysisViewModel.RootNode))
            {
                SyncVisualizerRoot();
            }
            else if (args.PropertyName == nameof(StorageAnalysisViewModel.SelectedNode))
            {
                SpaceVisualizer.SelectNode(ViewModel.SelectedNode);
                if (ViewModel.SelectedNode != null && !ItemsListView.SelectedItems.Contains(ViewModel.SelectedNode))
                {
                    ItemsListView.SelectedItem = ViewModel.SelectedNode;
                }
            }
        };

        ApplyAdaptiveLayout(ActualWidth);
    }

    private void SyncVisualizerRoot()
    {
        if (ViewModel.RootNode != null)
        {
            SpaceVisualizer.DataRoot = ViewModel.RootNode;
        }

        var targetNode = ViewModel.CurrentDirectoryNode ?? ViewModel.RootNode;
        if (targetNode != null)
        {
            SpaceVisualizer.SetVisualizationRoot(targetNode);
            SpaceVisualizer.SelectNode(ViewModel.SelectedNode);
        }
    }

    private void SpaceVisualizer_ItemSelected(object? sender, FileSystemNode node)
    {
        ViewModel.SelectedNode = node;
    }

    private void SpaceVisualizer_ItemDrilled(object? sender, FileSystemNode node)
    {
        if (node.IsDirectory)
        {
            ViewModel.CurrentDirectoryNode = node;
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string path && !string.IsNullOrEmpty(path))
        {
            ViewModel.SetPath(path);
        }
    }

    private void RefreshAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.RefreshCommand.CanExecute(null))
        {
            args.Handled = true;
            ViewModel.RefreshCommand.Execute(null);
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyAdaptiveLayout(e.NewSize.Width);
    }

    private void ApplyAdaptiveLayout(double width)
    {
        if (width < 920)
        {
            SetDetailsPanelVisibility(false);
        }
        else if (_isDetailsPanelOpen)
        {
            SetDetailsPanelVisibility(true);
        }
    }

    private void ToggleDetailsBtn_Click(object sender, RoutedEventArgs e)
    {
        _isDetailsPanelOpen = !_isDetailsPanelOpen;
        SetDetailsPanelVisibility(_isDetailsPanelOpen);
    }

    private void CloseDetails_Click(object sender, RoutedEventArgs e)
    {
        _isDetailsPanelOpen = false;
        SetDetailsPanelVisibility(false);
    }

    private void SetDetailsPanelVisibility(bool visible)
    {
        if (DetailsColumn != null && DetailsBorder != null)
        {
            DetailsColumn.Width = visible ? new GridLength(300) : new GridLength(0);
            DetailsBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ToggleChartBtn_Click(object sender, RoutedEventArgs e)
    {
        _isChartOpen = !_isChartOpen;
        if (VisualizerRow != null && SpaceVisualizer != null)
        {
            VisualizerRow.Height = _isChartOpen ? new GridLength(260) : new GridLength(0);
            SpaceVisualizer.Visibility = _isChartOpen ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnItemsListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ItemsListView.SelectedItems.OfType<FileSystemNode>().ToList();
        ViewModel.UpdateSelectedNodes(selected);
    }

    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ViewModel.ExecuteOpenSelected();
    }

    private void OnItemRowRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        // Select the row that was right-clicked so context menu commands target it immediately
        if (sender is FrameworkElement fe && fe.DataContext is FileSystemNode node)
        {
            // If the right-clicked row is already part of an active multi-selection, retain the entire selection!
            if (!ItemsListView.SelectedItems.Contains(node))
            {
                ItemsListView.SelectedItems.Clear();
                ItemsListView.SelectedItems.Add(node);
            }
        }
    }

    private async void OnListViewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var isCtrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        var isAlt = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        var isShift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

        if (e.Key == VirtualKey.Enter)
        {
            if (isAlt)
            {
                ViewModel.ShowPropertiesCommand.Execute(null);
            }
            else
            {
                ViewModel.ExecuteOpenSelected();
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Delete)
        {
            if (isShift)
            {
                await PermanentlyDeleteSelectedItemsAsync();
            }
            else
            {
                await RecycleSelectedItemsAsync();
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.A && isCtrl)
        {
            ItemsListView.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Back || (e.Key == VirtualKey.Up && isAlt))
        {
            ViewModel.ExecuteNavigateUp();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.C && isCtrl)
        {
            ViewModel.CopyPathCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.F && isCtrl)
        {
            SearchFilterBox.Focus(FocusState.Programmatic);
            SearchFilterBox.SelectAll();
            e.Handled = true;
        }
    }

    // Context Menu Handlers
    private void OnContextMenuOpen(object sender, RoutedEventArgs e)
    {
        ViewModel.ExecuteOpenSelected();
    }

    private void OnContextMenuOpenWith(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OpenWithCommand.CanExecute(null))
        {
            ViewModel.OpenWithCommand.Execute(null);
        }
    }

    private void OnContextMenuExplore(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedNode != null && ViewModel.SelectedNode.IsDirectory)
        {
            ViewModel.CurrentDirectoryNode = ViewModel.SelectedNode;
        }
    }

    private void OnContextMenuExplorer(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OpenInExplorerCommand.CanExecute(null))
        {
            ViewModel.OpenInExplorerCommand.Execute(null);
        }
    }

    private void OnContextMenuCopyPath(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CopyPathCommand.CanExecute(null))
        {
            ViewModel.CopyPathCommand.Execute(null);
        }
    }

    private void OnContextMenuScanFromHere(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ScanFromHereCommand.CanExecute(null))
        {
            ViewModel.ScanFromHereCommand.Execute(null);
        }
    }

    private void OnContextMenuProperties(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ShowPropertiesCommand.CanExecute(null))
        {
            ViewModel.ShowPropertiesCommand.Execute(null);
        }
    }

    private async void OnContextMenuRecycle(object sender, RoutedEventArgs e)
    {
        await RecycleSelectedItemsAsync();
    }

    private async void OnContextMenuPermanentDelete(object sender, RoutedEventArgs e)
    {
        await PermanentlyDeleteSelectedItemsAsync();
    }

    private async void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        await RecycleSelectedItemsAsync();
    }

    private async void OnPermanentDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        await PermanentlyDeleteSelectedItemsAsync();
    }

    private List<FileSystemNode> GetSelectedNodes()
    {
        if (ItemsListView.SelectedItems.Count > 0)
        {
            return ItemsListView.SelectedItems.OfType<FileSystemNode>().ToList();
        }
        if (ViewModel.SelectedNode != null)
        {
            return new List<FileSystemNode> { ViewModel.SelectedNode };
        }
        return new List<FileSystemNode>();
    }

    private async Task RecycleSelectedItemsAsync()
    {
        var nodes = GetSelectedNodes();
        if (nodes.Count == 0) return;

        if (nodes.Count == 1)
        {
            var node = nodes[0];
            if (string.IsNullOrEmpty(node.FullPath)) return;

            var confirmed = await ShowConfirmationDialogAsync(
                title: "Move to Recycle Bin",
                content: $"Are you sure you want to move '{node.Name}' ({node.FormattedSize}) to the Recycle Bin?",
                primaryButton: "Move to Recycle Bin");

            if (!confirmed) return;

            bool success = WindowsShellHelper.MoveToRecycleBin(node.FullPath);
            if (success)
            {
                ViewModel.RemoveNodeFromTree(node);
            }
            else
            {
                await ShowErrorDialogAsync("Recycle Bin Error", $"Failed to move '{node.Name}' to the Recycle Bin. The file may be in use or require elevated administrative permissions.");
            }
        }
        else
        {
            long totalBytes = nodes.Sum(n => n.SizeBytes);
            string formattedSize = FileSystemFormatter.FormatBytes(totalBytes);
            int dirCount = nodes.Count(n => n.IsDirectory);
            int fileCount = nodes.Count(n => !n.IsDirectory);

            string itemsDesc = dirCount > 0 && fileCount > 0
                ? $"{fileCount} file(s) and {dirCount} folder(s) ({formattedSize})"
                : $"{nodes.Count} items ({formattedSize})";

            var confirmed = await ShowConfirmationDialogAsync(
                title: "Move Multiple Items to Recycle Bin",
                content: $"Are you sure you want to move {itemsDesc} to the Recycle Bin?",
                primaryButton: $"Move {nodes.Count} Items to Recycle Bin");

            if (!confirmed) return;

            var successfullyDeleted = new List<FileSystemNode>();
            var failedNodes = new List<string>();

            foreach (var node in nodes)
            {
                if (WindowsShellHelper.MoveToRecycleBin(node.FullPath))
                {
                    successfullyDeleted.Add(node);
                }
                else
                {
                    failedNodes.Add(node.Name);
                }
            }

            if (successfullyDeleted.Count > 0)
            {
                ViewModel.RemoveNodesFromTree(successfullyDeleted);
            }

            if (failedNodes.Count > 0)
            {
                string failedSummary = failedNodes.Count <= 5
                    ? string.Join(", ", failedNodes)
                    : $"{string.Join(", ", failedNodes.Take(5))} and {failedNodes.Count - 5} more";

                await ShowErrorDialogAsync("Recycle Bin Warning",
                    $"Moved {successfullyDeleted.Count} items to Recycle Bin, but {failedNodes.Count} item(s) could not be moved:\n{failedSummary}\n\nThese items may be locked by another process or require elevated administrator privileges.");
            }
        }
    }

    private async Task PermanentlyDeleteSelectedItemsAsync()
    {
        var nodes = GetSelectedNodes();
        if (nodes.Count == 0) return;

        if (nodes.Count == 1)
        {
            var node = nodes[0];
            if (string.IsNullOrEmpty(node.FullPath)) return;

            var confirmed = await ShowConfirmationDialogAsync(
                title: "Permanently Delete",
                content: $"Are you sure you want to permanently delete '{node.Name}' ({node.FormattedSize})?\n\n⚠️ CAUTION: This item will be deleted immediately and CANNOT be restored from the Recycle Bin.",
                primaryButton: "Delete Permanently");

            if (!confirmed) return;

            bool success = WindowsShellHelper.DeletePermanently(node.FullPath);
            if (success)
            {
                ViewModel.RemoveNodeFromTree(node);
            }
            else
            {
                await ShowErrorDialogAsync("Delete Error", $"Failed to delete '{node.Name}'. The file may be locked by another process or require elevated administrative permissions.");
            }
        }
        else
        {
            long totalBytes = nodes.Sum(n => n.SizeBytes);
            string formattedSize = FileSystemFormatter.FormatBytes(totalBytes);
            int dirCount = nodes.Count(n => n.IsDirectory);
            int fileCount = nodes.Count(n => !n.IsDirectory);

            string itemsDesc = dirCount > 0 && fileCount > 0
                ? $"{fileCount} file(s) and {dirCount} folder(s) ({formattedSize})"
                : $"{nodes.Count} items ({formattedSize})";

            var confirmed = await ShowConfirmationDialogAsync(
                title: "Permanently Delete Multiple Items",
                content: $"Are you sure you want to permanently delete {itemsDesc}?\n\n⚠️ CAUTION: These items will be permanently erased immediately and CANNOT be recovered from the Recycle Bin.",
                primaryButton: $"Permanently Delete {nodes.Count} Items");

            if (!confirmed) return;

            var successfullyDeleted = new List<FileSystemNode>();
            var failedNodes = new List<string>();

            foreach (var node in nodes)
            {
                if (WindowsShellHelper.DeletePermanently(node.FullPath))
                {
                    successfullyDeleted.Add(node);
                }
                else
                {
                    failedNodes.Add(node.Name);
                }
            }

            if (successfullyDeleted.Count > 0)
            {
                ViewModel.RemoveNodesFromTree(successfullyDeleted);
            }

            if (failedNodes.Count > 0)
            {
                string failedSummary = failedNodes.Count <= 5
                    ? string.Join(", ", failedNodes)
                    : $"{string.Join(", ", failedNodes.Take(5))} and {failedNodes.Count - 5} more";

                await ShowErrorDialogAsync("Delete Warning",
                    $"Permanently deleted {successfullyDeleted.Count} items, but {failedNodes.Count} item(s) could not be deleted:\n{failedSummary}\n\nThese items may be locked by another process or require elevated administrator privileges.");
            }
        }
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string content, string primaryButton)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButton,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        catch
        {
            return false;
        }
    }

    private async Task ShowErrorDialogAsync(string title, string content)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch
        {
            // Ignore dialog display fallback
        }
    }
}

