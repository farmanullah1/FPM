using System.Text;

namespace PCManager.Core;

/// <summary>
/// Pure transforms for the marker-delimited FPM section of a hosts file. Kept free of file I/O so
/// the elevated helper and the UI read the same implementation and tests can exercise it.
/// </summary>
public static class HostsFileContent
{
    public const string BeginMarker = "# BEGIN FPM";
    public const string EndMarker = "# END FPM";

    /// <summary>Removes the FPM section (markers included) and leaves every other line intact.</summary>
    public static string RemoveBlock(string content)
    {
        string newline = DetectNewline(content);
        var lines = new List<string>();
        bool inside = false;

        foreach (string line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string trimmed = line.Trim();

            if (trimmed.Equals(BeginMarker, StringComparison.OrdinalIgnoreCase))
            {
                inside = true;
                continue;
            }

            if (trimmed.Equals(EndMarker, StringComparison.OrdinalIgnoreCase))
            {
                inside = false;
                continue;
            }

            if (!inside)
            {
                lines.Add(line);
            }
        }

        return string.Join(newline, lines).TrimEnd() + newline;
    }

    /// <summary>Replaces any existing FPM section with one entry per supplied domain.</summary>
    public static string ApplyBlock(string content, IReadOnlyList<string> domains)
    {
        string newline = DetectNewline(content);
        string without = RemoveBlock(content);
        var block = new StringBuilder();
        block.Append(BeginMarker).Append(newline);

        foreach (string domain in domains)
        {
            block.Append("0.0.0.0 ").Append(domain).Append(newline);
        }

        block.Append(EndMarker);

        return without.TrimEnd() + newline + newline + block + newline;
    }

    public static int CountBlockedDomains(string content)
    {
        bool inside = false;
        int count = 0;

        foreach (string line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string trimmed = line.Trim();

            if (trimmed.Equals(BeginMarker, StringComparison.OrdinalIgnoreCase))
            {
                inside = true;
                continue;
            }

            if (trimmed.Equals(EndMarker, StringComparison.OrdinalIgnoreCase))
            {
                inside = false;
                continue;
            }

            if (inside && trimmed.Length > 0 && !trimmed.StartsWith('#'))
            {
                count++;
            }
        }

        return count;
    }

    public static bool HasBlock(string content)
        => content.Contains(BeginMarker, StringComparison.OrdinalIgnoreCase);

    private static string DetectNewline(string content)
        => content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
