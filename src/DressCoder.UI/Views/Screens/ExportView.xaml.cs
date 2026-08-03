using System.Windows.Controls;
using DressCoder.UI.ViewModels.Screens;
using Microsoft.Win32;

namespace DressCoder.UI.Views.Screens;

public partial class ExportView : UserControl
{
    public ExportView()
    {
        InitializeComponent();
    }

    private void OnBrowseOutputClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Elegí la carpeta de salida" };

        if (dialog.ShowDialog() == true && DataContext is ExportViewModel viewModel)
        {
            viewModel.OutputDirectory = dialog.FolderName;
        }
    }
}
