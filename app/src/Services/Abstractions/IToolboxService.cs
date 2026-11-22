using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Small Windows utilities exposed by the Toolbox.</summary>
public interface IToolboxService
{
    IReadOnlyList<ToolboxTool> GetTools();

    /// <summary>Runs the tool and returns a message for the UI.</summary>
    Task<OperationResult> LaunchAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>Captures all screens, saves a PNG and returns its path (null on failure).</summary>
    Task<string?> TakeScreenshotAsync(CancellationToken cancellationToken = default);
}
