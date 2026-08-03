namespace DressCoder.UI.Navigation;

/// <summary>A single entry in the shell's sidebar menu.</summary>
public sealed class NavigationItem
{
    public required string Title { get; init; }

    /// <summary>Simple unicode glyph used as icon, avoiding a dependency on an icon font/pack.</summary>
    public required string Glyph { get; init; }

    public required Type ViewModelType { get; init; }
}
