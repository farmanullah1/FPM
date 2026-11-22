using System.Diagnostics;
using System.Text;

namespace PCManager.Services.Infrastructure;

/// <summary>Result of a console command.</summary>
internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    internal bool Success => ExitCode == 0;
    internal string Message => string.IsNullOrWhiteSpace(StandardError) ? StandardOutput.Trim() : StandardError.Trim();
}

/// <summary>Runs console tools (schtasks / powershell) without showing a window.</summary>
internal static class ProcessRunner
{
    internal static ProcessResult Run(string fileName, string arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return new ProcessResult(-1, string.Empty, $"{Path.GetFileName(fileName)} timed out.");
            }

            process.WaitForExit();
            return new ProcessResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// Runs a Windows PowerShell script. The script is written to a temporary .ps1 file
    /// so quoting never has to fight with cmd-style argument escaping.
    /// </summary>
    internal static ProcessResult RunPowerShell(string script, TimeSpan timeout)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"pcmanager-{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(tempFile, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");

            return Run(powershell, $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tempFile}\"", timeout);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort */ }
        }
    }
}
