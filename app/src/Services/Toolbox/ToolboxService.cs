using System.Drawing;
using System.Drawing.Imaging;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;
using PCManager.Services.Interop;

namespace PCManager.Services.Toolbox;

/// <summary>
/// Toolbox entries. Every tool performs a real action:
///   - Screenshot  : GDI BitBlt of the whole virtual desktop into a PNG.
///   - Recorder    : opens the built-in Xbox Game Bar recorder (ms-gamebar:), falling back
///                   to the Windows Steps Recorder (psr.exe) when Game Bar is unavailable.
///   - Calculator  : calc.exe through the shell.
///   - Notepad     : notepad.exe.
///   - DNS flush   : ipconfig /flushdns, the documented way to clear the resolver cache.
///   - Quick links : opens the everyday web destinations in the default browser.
/// </summary>
public sealed class ToolboxService : IToolboxService
{
    private static readonly string[] QuickLinks =
    {
        "https://www.bing.com/",
        "https://github.com/",
        "https://learn.microsoft.com/"
    };

    private readonly IToolLauncher _launcher;
    private readonly IAppLogger _logger;

    public ToolboxService(IToolLauncher launcher, IAppLogger logger)
    {
        _launcher = launcher;
        _logger = logger;
    }

    public IReadOnlyList<ToolboxTool> GetTools() => new List<ToolboxTool>
    {
        new() { Id = "screenshot", Title = "Screenshot", Description = "Capture every screen and save a PNG.", IconKey = "Icon.Screenshot" },
        new() { Id = "screen-recorder", Title = "Screen recorder", Description = "Record the screen with Windows Game Bar.", IconKey = "Icon.Record" },
        new() { Id = "calculator", Title = "Calculator", Description = "Open the Windows calculator.", IconKey = "Icon.Calculator" },
        new() { Id = "notepad", Title = "Notepad", Description = "Open a blank text document.", IconKey = "Icon.Notepad" },
        new() { Id = "dns-flush", Title = "DNS / network flush", Description = "Clear the DNS cache and renew the resolver state.", IconKey = "Icon.Network" },
        new() { Id = "quick-links", Title = "Quick web links", Description = "Open Bing, GitHub and Microsoft Learn.", IconKey = "Icon.Links" }
    };

    public async Task<OperationResult> LaunchAsync(string toolId, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (toolId)
            {
                case "screenshot":
                    string? path = await TakeScreenshotAsync(cancellationToken).ConfigureAwait(false);
                    if (path is null)
                    {
                        return OperationResult.Fail("The screenshot could not be captured.");
                    }

                    _launcher.Start(path);
                    _logger.Info($"Screenshot saved to {path}.");
                    return OperationResult.Ok($"Screenshot saved to {path}");

                case "screen-recorder":
                    return StartRecorder();

                case "calculator":
                    _launcher.Start("calc.exe");
                    return OperationResult.Ok("Calculator opened.");

                case "notepad":
                    _launcher.Start("notepad.exe");
                    return OperationResult.Ok("Notepad opened.");

                case "dns-flush":
                    ProcessResult result = ProcessRunner.Run("ipconfig.exe", "/flushdns", TimeSpan.FromSeconds(20));
                    _logger.Info($"DNS flush exit code {result.ExitCode}.");
                    return result.Success
                        ? OperationResult.Ok("DNS resolver cache flushed.")
                        : OperationResult.Fail(result.Message.Length > 0 ? result.Message : "The flush command failed.");

                case "quick-links":
                    foreach (string link in QuickLinks)
                    {
                        _launcher.OpenUri(link);
                    }

                    return OperationResult.Ok("Opened quick links in your browser.");

                default:
                    return OperationResult.Fail($"Unknown tool '{toolId}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Tool '{toolId}' failed.", ex);
            return OperationResult.Fail(ex.Message);
        }
    }

    /// <summary>Game Bar records the foreground window or the whole screen to MP4.</summary>
    private OperationResult StartRecorder()
    {
        try
        {
            _launcher.OpenUri("ms-gamebar:");
            return OperationResult.Ok("Game Bar opened - press the record button (or Win+Alt+R) to start recording.");
        }
        catch (Exception)
        {
            // Game Bar can be removed by policy; Steps Recorder ships with Windows.
            _launcher.Start("psr.exe");
            return OperationResult.Ok("Game Bar is unavailable, so the Windows Steps Recorder was opened instead.");
        }
    }

    private Task<string?> TakeScreenshotAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        try
        {
            // Virtual desktop bounds cover all monitors, including monitors left of the
            // primary one (negative X).
            int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Farmanullah PC Manager Screenshots");

            Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            using var bitmap = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            bitmap.Save(file, ImageFormat.Png);
            return file;
        }
        catch (Exception)
        {
            return null;
        }
    }, cancellationToken);
}
