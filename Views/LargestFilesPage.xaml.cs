using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Views;

public sealed partial class LargestFilesPage : Page
{
    public LargestFilesViewModel ViewModel => AppServices.LargestFilesViewModel;

    public LargestFilesPage()
    {
        InitializeComponent();
    }

    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.OpenFileCommand.CanExecute(null))
        {
            ViewModel.OpenFileCommand.Execute(null);
        }
    }

    private void OnContextMenuOpenFile(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OpenFileCommand.CanExecute(null))
        {
            ViewModel.OpenFileCommand.Execute(null);
        }
    }

    private void OnContextMenuOpenExplorer(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OpenInExplorerCommand.CanExecute(null))
        {
            ViewModel.OpenInExplorerCommand.Execute(null);
        }
    }

    private void OnContextMenuGoToFolder(object sender, RoutedEventArgs e)
    {
        if (ViewModel.NavigateToFolderCommand.CanExecute(null))
        {
            ViewModel.NavigateToFolderCommand.Execute(null);
        }
    }
}

