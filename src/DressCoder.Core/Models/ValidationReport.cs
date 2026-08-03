namespace DressCoder.Core.Models;

/// <summary>Severity of a validation finding, shown in the UI's warnings/errors panel.</summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// A single validation finding produced by the Validator module (broken references,
/// naming conflicts, missing metadata, hard Dresscode rule violations). See
/// docs/02-documento-tecnico.md section 4 for the hard rules checked here.
/// </summary>
public sealed class ValidationIssue
{
    public required ValidationSeverity Severity { get; init; }
    public required string Message { get; init; }

    /// <summary>Internal path or chunk related to this issue, if applicable.</summary>
    public string? RelatedPath { get; init; }

    /// <summary>Whether the app can auto-fix this issue without user input.</summary>
    public bool CanAutoFix { get; init; }
}

/// <summary>Aggregate validation outcome for a conversion, gating the Export step.</summary>
public sealed class ValidationReport
{
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }

    public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == ValidationSeverity.Warning);
}
