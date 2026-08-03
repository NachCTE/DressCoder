using DressCoder.Core.Models;

namespace DressCoder.Application.Services;

/// <summary>
/// Holds the in-progress conversion state shared across screens (Import → Configuration →
/// Export). Registered as a singleton so every screen ViewModel sees the same instance
/// regardless of navigation order (see <c>INavigationService</c> in DressCoder.UI, which
/// caches one ViewModel instance per type — this service is the actual shared state between
/// those cached instances).
/// </summary>
public interface IConversionSessionState
{
    /// <summary>Path to the replacer .pak/.utoc/.ucas (or folder) last imported successfully.</summary>
    string? SourcePath { get; set; }

    /// <summary>Display name of the last imported replacer project, for the UI to show.</summary>
    string? SourceName { get; set; }

    /// <summary>User-provided metadata collected in the "Configuración" screen.</summary>
    ModMetadataInput? Metadata { get; set; }
}

/// <inheritdoc cref="IConversionSessionState"/>
public sealed class ConversionSessionState : IConversionSessionState
{
    public string? SourcePath { get; set; }
    public string? SourceName { get; set; }
    public ModMetadataInput? Metadata { get; set; }
}
