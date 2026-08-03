using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace DressCoder.UI.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, ObservableObject> _cache = new();

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public ObservableObject? CurrentViewModel { get; private set; }

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo(Type viewModelType)
    {
        if (!_cache.TryGetValue(viewModelType, out var viewModel))
        {
            viewModel = (ObservableObject)_services.GetRequiredService(viewModelType);
            _cache[viewModelType] = viewModel;
        }

        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject => NavigateTo(typeof(TViewModel));
}
