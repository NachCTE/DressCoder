using System.Windows;
using DressCoder.UI.ViewModels;

namespace DressCoder.UI;

/// <summary>
/// Shell window: hosts the sidebar navigation and the current screen's view
/// (via DataTemplates registered in App.xaml). All navigation logic lives in
/// <see cref="MainViewModel"/> / <see cref="Navigation.INavigationService"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnNavigationItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Type viewModelType } && DataContext is MainViewModel viewModel)
        {
            viewModel.NavigateTo(viewModelType);
        }
    }
}