using System.Windows.Controls;
using DressCoder.UI.ViewModels.Screens;
using Microsoft.Win32;

namespace DressCoder.UI.Views.Screens;

public partial class ImportView : UserControl
{
    public ImportView()
    {
        InitializeComponent();
    }

    private void OnPickFileClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegí el archivo del replacer",
            Filter = "Archivos de mod IoStore (*.pak;*.utoc;*.ucas)|*.pak;*.utoc;*.ucas|Todos los archivos|*.*",
        };

        if (dialog.ShowDialog() == true && DataContext is ImportViewModel viewModel)
        {
            viewModel.ImportCommand.Execute(dialog.FileName);
        }
    }

    private void OnPickFolderClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Elegí la carpeta del mod" };

        if (dialog.ShowDialog() == true && DataContext is ImportViewModel viewModel)
        {
            viewModel.ImportCommand.Execute(dialog.FolderName);
        }
    }
}
