using CommunityToolkit.Mvvm.ComponentModel;
using DressCoder.UI.Navigation;
using DressCoder.UI.ViewModels.Screens;

namespace DressCoder.UI.ViewModels;

/// <summary>Shell ViewModel: owns the sidebar's navigation items and reflects the current screen.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ObservableObject? currentViewModel;

    public IReadOnlyList<NavigationItem> NavigationItems { get; } =
    [
        new NavigationItem { Title = "Home", Glyph = "🏠", ViewModelType = typeof(HomeViewModel) },
        new NavigationItem { Title = "Importar Mod", Glyph = "📥", ViewModelType = typeof(ImportViewModel) },
        new NavigationItem { Title = "Análisis", Glyph = "🔍", ViewModelType = typeof(AnalysisViewModel) },
        new NavigationItem { Title = "Configuración", Glyph = "⚙", ViewModelType = typeof(ConfigurationViewModel) },
        new NavigationItem { Title = "Vista previa", Glyph = "👁", ViewModelType = typeof(PreviewViewModel) },
        new NavigationItem { Title = "Exportación", Glyph = "📦", ViewModelType = typeof(ExportViewModel) },
        new NavigationItem { Title = "Log de errores", Glyph = "📋", ViewModelType = typeof(LogViewModel) },
    ];

    public MainViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.CurrentViewModelChanged += (_, _) => CurrentViewModel = _navigation.CurrentViewModel;
        _navigation.NavigateTo<HomeViewModel>();
    }

    public void NavigateTo(Type viewModelType) => _navigation.NavigateTo(viewModelType);
}
