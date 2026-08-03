using System.Windows.Controls;
using DressCoder.UI.ViewModels.Screens;
using Microsoft.Win32;

namespace DressCoder.UI.Views.Screens;

public partial class ConfigurationView : UserControl
{
    public ConfigurationView()
    {
        InitializeComponent();
    }

    private void OnBrowseIconClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegí un ícono PNG",
            Filter = "Imágenes PNG (*.png)|*.png",
        };

        if (dialog.ShowDialog() == true && DataContext is ConfigurationViewModel viewModel)
        {
            viewModel.IconPath = dialog.FileName;
        }
    }
}
