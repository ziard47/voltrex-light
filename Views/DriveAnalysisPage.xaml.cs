using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Views;

public sealed partial class DriveAnalysisPage : Page
{
    public DriveAnalysisViewModel ViewModel => AppServices.DriveAnalysisViewModel;

    public DriveAnalysisPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.RootNode != null)
        {
            SpaceVisualizer.DataRoot = ViewModel.RootNode;
            SpaceVisualizer.SelectNode(ViewModel.SelectedNode);
        }

        ViewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(DriveAnalysisViewModel.RootNode))
            {
                SpaceVisualizer.DataRoot = ViewModel.RootNode;
            }
            else if (args.PropertyName == nameof(DriveAnalysisViewModel.SelectedNode))
            {
                SpaceVisualizer.SelectNode(ViewModel.SelectedNode);
            }
        };
    }

    private void SpaceVisualizer_ItemSelected(object? sender, FileSystemNode node)
    {
        ViewModel.SelectedNode = node;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string path && !string.IsNullOrEmpty(path))
        {
            ViewModel.SetPath(path);
        }
    }
}

