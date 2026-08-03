using CommunityToolkit.Mvvm.ComponentModel;

namespace DressCoder.UI.ViewModels.Screens;

/// <summary>
/// "Vista previa" screen placeholder: will show the assembled plugin folder tree before
/// export, once IPluginAssembler (Etapa 5) is implemented.
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string message = "Acá se va a previsualizar la estructura del plugin generado " +
                              "({PluginName}.uplugin, Resources/Icon.png, Content/Paks/WindowsNoEditor/*) antes de exportar.";
}
