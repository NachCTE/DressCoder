using CommunityToolkit.Mvvm.ComponentModel;

namespace DressCoder.UI.Navigation;

/// <summary>
/// Minimal ViewModel-first navigation service for the shell: resolves screen ViewModels
/// through DI (so they get their dependencies injected) and caches one instance per
/// ViewModel type so screen state (e.g. an in-progress import) survives switching tabs.
/// </summary>
public interface INavigationService
{
    ObservableObject? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    void NavigateTo(Type viewModelType);

    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
}
