using CommunityToolkit.Mvvm.ComponentModel;

namespace DressCoder.UI.ViewModels.Screens;

/// <summary>
/// "Análisis" screen placeholder. Will show classified assets + detected character/outfit
/// once <see cref="DressCoder.Core.Analyzer.IModAnalyzer"/> is implemented (Etapa 6 —
/// Automatización). The navigation shell and screen wiring are already in place.
/// </summary>
public partial class AnalysisViewModel : ObservableObject
{
    [ObservableProperty]
    private string message = "El análisis automático de personaje/outfit/assets se implementa en la Etapa 6 " +
                              "(Automatización), una vez exista IModAnalyzer. Esta pantalla ya está conectada a la navegación.";
}
