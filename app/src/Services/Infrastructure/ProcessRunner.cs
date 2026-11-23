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
        => Execute(new ProcessStartInfo(fileName, arguments), fileName, timeout);

    private static ProcessResult Execute(ProcessStartInfo startInfo, string displayName, TimeSpan timeout)
    {
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        try
        {
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {displayName}.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return new ProcessResult(-1, string.Empty, $"{Path.GetFileName(displayName)} timed out.");
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

    /// <summary>
    /// Runs a fixed Windows PowerShell script with typed arguments. Used when a value (for
    /// example a package full name) must reach the script without becoming command text.
    /// </summary>
    internal static ProcessResult RunPowerShell(
        string script,
        IReadOnlyList<string> arguments,
        TimeSpan timeout)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"pcmanager-{Guid.NewGuid():N}.ps1");

        try
        {
            File.WriteAllText(tempFile, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");

            var startInfo = new ProcessStartInfo(powershell);
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(tempFile);

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return Execute(startInfo, powershell, timeout);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort */ }
        }
    }
}
