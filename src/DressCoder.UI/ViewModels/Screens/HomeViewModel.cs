using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DressCoder.Application.Services;
using DressCoder.UI.Navigation;

namespace DressCoder.UI.ViewModels.Screens;

/// <summary>Landing screen: shows environment/tool status and quick links into the workflow.</summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly IStartupDiagnosticsService _diagnostics;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string toolsStatusMessage = "Verificando herramientas externas...";

    [ObservableProperty]
    private bool toolsOk;

    public HomeViewModel(IStartupDiagnosticsService diagnostics, INavigationService navigation)
    {
        _diagnostics = diagnostics;
        _navigation = navigation;
        RefreshDiagnostics();
    }

    [RelayCommand]
    private void RefreshDiagnostics()
    {
        var result = _diagnostics.CheckExternalTools();
        ToolsOk = result.AllToolsAvailable;
        ToolsStatusMessage = result.AllToolsAvailable
            ? "✔ retoc.exe y repak.exe encontrados correctamente."
            : "⚠ Faltan herramientas externas. Ejecutá tools/download-tools.ps1." +
              (result.RetocError is not null ? $"\n- {result.RetocError}" : "") +
              (result.RepakError is not null ? $"\n- {result.RepakError}" : "");
    }

    [RelayCommand]
    private void GoToImport() => _navigation.NavigateTo<ImportViewModel>();
}
