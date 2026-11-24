using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Interop;

namespace PCManager.Services.Protection;

/// <summary>
/// Read-only window scan. Only windows that are visible, titled, not tool windows and owned by
/// a non-system process are reported. The allow list is a local JSON file, never sent anywhere.
/// </summary>
public sealed class PopupMonitorService : IPopupMonitorService
{
    private static readonly HashSet<string> HostProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost",
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "TextInputHost",
        "SystemSettings"
    };

    private readonly IAppLogger _logger;
    private readonly string _allowListPath;
    private readonly string _historyPath;
    private readonly HashSet<string> _allowed;
    private readonly Dictionary<string, List<DateTime>> _history;
    private readonly object _gate = new();
    private readonly NativeMethods.EnumWindowsProc _callback;
    private readonly List<WindowEntry> _windows = new();
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromDays(30);

    public PopupMonitorService(IAppLogger logger)
    {
        _logger = logger;
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FPM");
        _allowListPath = Path.Combine(directory, "popup-allow.json");
        _historyPath = Path.Combine(directory, "popup-history.json");
        _allowed = LoadAllowList();
        _history = LoadHistory();
        _callback = CollectWindow;
    }

    public Task<IReadOnlyList<PopupSourceInfo>> ScanAsync(CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<PopupSourceInfo>>(() =>
        {
            var grouped = new Dictionary<string, (string Title, int Pid, int Count)>(StringComparer.OrdinalIgnoreCase);

            lock (_gate)
            {
                _windows.Clear();
                NativeMethods.EnumWindows(_callback, IntPtr.Zero);

                foreach (WindowEntry window in _windows)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using Process process = Process.GetProcessById(window.ProcessId);
                        string name = process.ProcessName;

                        if (string.IsNullOrWhiteSpace(name) ||
                            name.Equals("FPM", StringComparison.OrdinalIgnoreCase) ||
                            AppInfo.IsProtectedProcessName(name) ||
                            HostProcesses.Contains(name))
                        {
                            continue;
                        }

                        if (grouped.TryGetValue(name, out var existing))
                        {
                            grouped[name] = (existing.Title, existing.Pid, existing.Count + 1);
                        }
                        else
                        {
                            grouped[name] = (window.Title, window.ProcessId, 1);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // The process exited between enumeration and lookup.
                    }
                    catch (Exception ex)
                    {
                        _logger.Info($"Popup scan skipped a window: {ex.Message}");
                    }
                }

                _windows.Clear();
            }

            Dictionary<string, int> seen;

            lock (_gate)
            {
                seen = RecordSightings(grouped.Keys);
            }

            return grouped
                .Select(pair => new PopupSourceInfo(
                    pair.Key,
                    pair.Value.Title,
                    pair.Value.Pid,
                    pair.Value.Count,
                    _allowed.Contains(pair.Key),
                    seen.TryGetValue(pair.Key, out int count) ? count : 0))
                .OrderByDescending(source => source.WindowCount)
                .ThenBy(source => source.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToList();
        }, cancellationToken);

    public Task<bool> IsAllowedAsync(string processName)
        => Task.FromResult(_allowed.Contains(processName));

    public Task SetAllowedAsync(string processName, bool allowed)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return Task.CompletedTask;
        }

        if (allowed)
        {
            _allowed.Add(processName);
        }
        else
        {
            _allowed.Remove(processName);
        }

        SaveAllowList();
        return Task.CompletedTask;
    }

    private bool CollectWindow(IntPtr hWnd, IntPtr lParam)
    {
        try
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            int length = NativeMethods.GetWindowTextLength(hWnd);

            if (length <= 0)
            {
                return true;
            }

            int extendedStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);

            if ((extendedStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);

            if (processId == 0 || processId == Environment.ProcessId)
            {
                return true;
            }

            var title = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, title, title.Capacity);

            string text = title.ToString().Trim();

            if (text.Length > 0)
            {
                _windows.Add(new WindowEntry((int)processId, text.Length > 120 ? text[..120] : text));
            }
        }
        catch (Exception ex)
        {
            _logger.Info($"Popup window enumeration skipped one window: {ex.Message}");
        }

        return true;
    }

    private HashSet<string> LoadAllowList()
    {
        try
        {
            if (!File.Exists(_allowListPath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            List<string>? entries = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_allowListPath));
            return new HashSet<string>(entries ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.Error("Could not read the popup allow list.", ex);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveAllowList()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_allowListPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _allowListPath,
                JsonSerializer.Serialize(_allowed.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            _logger.Error("Could not save the popup allow list.", ex);
        }
    }

    /// <summary>Counts one sighting per app per scan, keeping only the last 30 days.</summary>
    private Dictionary<string, int> RecordSightings(IEnumerable<string> names)
    {
        DateTime cutoff = DateTime.UtcNow - HistoryWindow;
        DateTime now = DateTime.UtcNow;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (string name in names)
        {
            if (!_history.TryGetValue(name, out List<DateTime>? dates))
            {
                dates = new List<DateTime>();
                _history[name] = dates;
            }

            dates.RemoveAll(date => date < cutoff);
            dates.Add(now);
            counts[name] = dates.Count;
        }

        foreach (string expired in _history.Where(pair => pair.Value.TrueForAll(date => date < cutoff))
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _history.Remove(expired);
        }

        SaveHistory();
        return counts;
    }

    private Dictionary<string, List<DateTime>> LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                return new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, List<string>>? raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                File.ReadAllText(_historyPath));
            var history = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

            foreach ((string name, List<string> dates) in raw ?? new())
            {
                history[name] = dates
                    .Select(value => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date) ? date : (DateTime?)null)
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value)
                    .ToList();
            }

            return history;
        }
        catch (Exception ex)
        {
            _logger.Error("Could not read the popup sighting history.", ex);
            return new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveHistory()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_historyPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializable = _history.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select(date => date.ToString("O", CultureInfo.InvariantCulture)).ToList(),
                StringComparer.OrdinalIgnoreCase);

            File.WriteAllText(_historyPath, JsonSerializer.Serialize(serializable));
        }
        catch (Exception ex)
        {
            _logger.Error("Could not save the popup sighting history.", ex);
        }
    }

    private readonly record struct WindowEntry(int ProcessId, string Title);
}
