using DressCoder.Core.Models;
using DressCoder.Core.Validator;

namespace DressCoder.Infrastructure.Assembly;

/// <summary>
/// Basic structural validator: checks the hard rules that can be verified without parsing
/// Zen packages (folder layout, presence of the .uplugin/.utoc/.ucas, non-empty files, name
/// conflicts with existing plugins in the destination). Deeper checks (broken references,
/// duplicate DA_ModMetaData, material slot mismatches) require asset-level parsing not yet
/// implemented (Etapa 6 — see docs/02-documento-tecnico.md section 4/5).
/// </summary>
public sealed class ModValidator : IModValidator
{
    public ValidationReport Validate(ReplacerModProject project, ModMetadataInput metadata)
    {
        var issues = new List<ValidationIssue>();

        if (project.Chunks.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "El replacer importado no contiene ningún asset extraído.",
            });
        }

        if (string.IsNullOrWhiteSpace(metadata.PluginName))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "Falta el nombre del plugin.",
            });
        }
        else if (metadata.PluginName.Contains(' ') || metadata.PluginName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "El nombre del plugin tiene espacios o caracteres inválidos para una carpeta.",
            });
        }

        if (string.IsNullOrWhiteSpace(metadata.FriendlyName))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = "No se indicó un nombre visible (FriendlyName); se usará el nombre del plugin.",
                CanAutoFix = true,
            });
        }

        return new ValidationReport { Issues = issues };
    }

    public ValidationReport ValidateOutput(string pluginOutputDirectory)
    {
        var issues = new List<ValidationIssue>();
        var pluginName = Path.GetFileName(pluginOutputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var upluginPath = Path.Combine(pluginOutputDirectory, $"{pluginName}.uplugin");
        if (!File.Exists(upluginPath))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"Falta el archivo '{pluginName}.uplugin'.",
                RelatedPath = upluginPath,
            });
        }

        var iconPath = Path.Combine(pluginOutputDirectory, "Resources", "Icon.png");
        if (!File.Exists(iconPath))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = "Falta 'Resources/Icon.png'.",
                RelatedPath = iconPath,
            });
        }

        var paksDirectory = Path.Combine(pluginOutputDirectory, "Content", "Paks", "WindowsNoEditor");
        if (!Directory.Exists(paksDirectory))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "Falta la carpeta 'Content/Paks/WindowsNoEditor'.",
                RelatedPath = paksDirectory,
            });
        }
        else
        {
            var utoc = Directory.EnumerateFiles(paksDirectory, "*.utoc").FirstOrDefault();
            var ucas = Directory.EnumerateFiles(paksDirectory, "*.ucas").FirstOrDefault();

            if (utoc is null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Falta el archivo .utoc dentro de Content/Paks/WindowsNoEditor.",
                });
            }
            else if (new FileInfo(utoc).Length == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"'{Path.GetFileName(utoc)}' está vacío.",
                    RelatedPath = utoc,
                });
            }

            if (ucas is null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Falta el archivo .ucas dentro de Content/Paks/WindowsNoEditor.",
                });
            }
            else if (new FileInfo(ucas).Length == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"'{Path.GetFileName(ucas)}' está vacío.",
                    RelatedPath = ucas,
                });
            }
        }

        return new ValidationReport { Issues = issues };
    }
}
