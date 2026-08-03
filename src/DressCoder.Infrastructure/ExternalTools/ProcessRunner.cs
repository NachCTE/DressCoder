using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DressCoder.Infrastructure.ExternalTools;

/// <summary>Outcome of running an external process: exit code plus captured output streams.</summary>
public sealed record ProcessExecutionResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Thin wrapper around <see cref="Process"/> for invoking external CLI tools (repak/retoc),
/// capturing stdout/stderr and supporting cancellation. Kept separate from the tool-specific
/// readers/builders so it can be unit-tested and reused independently.
/// </summary>
public sealed class ProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessExecutionResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        var argumentList = arguments.ToList();
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in argumentList)
        {
            psi.ArgumentList.Add(arg);
        }

        _logger.LogDebug("Ejecutando: {FileName} {Args}", fileName, string.Join(' ', argumentList));

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdOutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stdErrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var result = new ProcessExecutionResult(process.ExitCode, stdOutBuilder.ToString(), stdErrBuilder.ToString());
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Proceso {FileName} finalizó con código {ExitCode}. StdErr: {StdErr}",
                Path.GetFileName(fileName), result.ExitCode, result.StdErr);
        }

        return result;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup on cancellation; nothing actionable if this fails.
        }
    }
}
