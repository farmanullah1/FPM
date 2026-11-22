using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// The in-app approval gate. Only three forms produce a plan:
/// <c>proceed</c> (every ticked row), <c>only 1, 2</c> and <c>keep 1, 2</c>.
/// Ranges work, duplicates are rejected, unknown words produce a clarification.
/// Opt-in rows are never added implicitly; <c>only</c> is the explicit way to choose one.
/// </summary>
public sealed class CleanupCommandParser : ICleanupCommandParser
{
    private const string Forms =
        "Use \"proceed\", \"only 1, 2\" or \"keep 1, 2\". Ranges such as \"only 3-7\" also work.";

    public CleanupCommandResult Parse(string command, CleanupScanResult scan)
    {
        ArgumentNullException.ThrowIfNull(scan);

        if (scan.Items.Count == 0)
        {
            return Clarify("There is nothing to approve. Scan first.");
        }

        string text = (command ?? string.Empty).Trim().ToLowerInvariant();

        if (text.Length == 0)
        {
            return Clarify(Forms);
        }

        if (text == "proceed")
        {
            List<CleanupCandidate> ticked = scan.Items
                .Where(item => item.IsSelected && item.Bytes > 0)
                .ToList();

            if (ticked.Count == 0)
            {
                return Clarify("Nothing is ticked. Tick the rows you want, or use \"only 1, 2\".");
            }

            return Approve(ticked);
        }

        if (text.StartsWith("only ", StringComparison.Ordinal) ||
            text.StartsWith("keep ", StringComparison.Ordinal))
        {
            bool only = text.StartsWith("only ", StringComparison.Ordinal);
            string body = text[(only ? 5 : 5)..].Trim();

            if (!TryParseNumbers(body, scan.Items.Count, out List<int> numbers, out string error))
            {
                return Clarify(error);
            }

            var requested = new HashSet<int>(numbers);

            List<CleanupCandidate> chosen = only
                ? scan.Items.Where(item => requested.Contains(item.Number)).ToList()
                : scan.Items.Where(item =>
                        !requested.Contains(item.Number) &&
                        item.Bytes > 0 &&
                        (item.Tier != CleanupTier.OptIn || item.IsSelected))
                    .ToList();

            if (chosen.Count == 0)
            {
                return Clarify(only
                    ? "That command selects no rows."
                    : "That command would leave nothing to delete.");
            }

            return Approve(chosen);
        }

        return Clarify($"\"{command}\" is not one of the three approval commands. {Forms}");
    }

    private static CleanupCommandResult Approve(IReadOnlyList<CleanupCandidate> candidates)
    {
        var items = candidates
            .OrderBy(candidate => candidate.Number)
            .Select(candidate => new CleanupPlanItem(
                candidate.Number,
                candidate.Rule.Id,
                candidate.Rule.Name,
                candidate.Roots,
                candidate.Bytes,
                candidate.Rule.Tier,
                candidate.Rule.Action,
                candidate.Rule.MinimumAge,
                candidate.Rule.RequiresElevation,
                candidate.Rule.RequiresAppClosed,
                candidate.Rule.ProcessName))
            .ToList();

        var plan = new CleanupPlan { Items = items };

        return new CleanupCommandResult
        {
            Kind = CleanupCommandKind.Approved,
            Plan = plan,
            Message = $"You are about to delete {items.Count} item(s), {ByteFormatter.Format(plan.TotalBytes)}."
        };
    }

    private static CleanupCommandResult Clarify(string message)
        => new()
        {
            Kind = CleanupCommandKind.Clarification,
            Message = message
        };

    private static bool TryParseNumbers(string body, int maximum, out List<int> numbers, out string error)
    {
        numbers = new List<int>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(body))
        {
            error = Forms;
            return false;
        }

        var seen = new HashSet<int>();
        string[] tokens = body.Split(
            new[] { ',', ' ', ';', '\t' },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            if (token.Contains('-', StringComparison.Ordinal))
            {
                string[] bounds = token.Split('-', StringSplitOptions.RemoveEmptyEntries);

                if (bounds.Length != 2 ||
                    !int.TryParse(bounds[0], out int from) ||
                    !int.TryParse(bounds[1], out int to))
                {
                    error = $"\"{token}\" is not a number or range. {Forms}";
                    return false;
                }

                if (from < 1 || to < 1 || from > maximum || to > maximum)
                {
                    error = $"Numbers must be between 1 and {maximum}.";
                    return false;
                }

                if (to < from)
                {
                    error = $"The range \"{token}\" is backwards.";
                    return false;
                }

                for (int value = from; value <= to; value++)
                {
                    if (!seen.Add(value))
                    {
                        error = $"Number {value} appears twice. {Forms}";
                        return false;
                    }

                    numbers.Add(value);
                }

                continue;
            }

            if (!int.TryParse(token, out int number))
            {
                error = $"\"{token}\" is not a number. {Forms}";
                return false;
            }

            if (number < 1 || number > maximum)
            {
                error = $"Numbers must be between 1 and {maximum}.";
                return false;
            }

            if (!seen.Add(number))
            {
                error = $"Number {number} appears twice. {Forms}";
                return false;
            }

            numbers.Add(number);
        }

        if (numbers.Count == 0)
        {
            error = Forms;
            return false;
        }

        return true;
    }
}
