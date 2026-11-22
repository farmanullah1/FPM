using PCManager.Core.Models;
using PCManager.Services.Cleanup;
using Xunit;

namespace PCManager.Tests;

public sealed class CleanupCommandParserTests
{
    private readonly CleanupCommandParser _parser = new();

    [Fact]
    public void Proceed_approves_only_ticked_rows()
    {
        CleanupScanResult scan = Scan((1, true, CleanupTier.Safe), (2, false, CleanupTier.Caution), (3, true, CleanupTier.Safe));

        CleanupCommandResult result = _parser.Parse("proceed", scan);

        Assert.Equal(CleanupCommandKind.Approved, result.Kind);
        Assert.NotNull(result.Plan);
        Assert.Equal(new[] { 1, 3 }, result.Plan!.Items.Select(item => item.Number));
    }

    [Fact]
    public void Only_accepts_comma_separated_numbers()
    {
        CleanupScanResult scan = Scan((1, true, CleanupTier.Safe), (2, true, CleanupTier.Safe), (3, true, CleanupTier.Safe));

        CleanupCommandResult result = _parser.Parse("only 1, 2", scan);

        Assert.NotNull(result.Plan);
        Assert.Equal(new[] { 1, 2 }, result.Plan!.Items.Select(item => item.Number));
    }

    [Fact]
    public void Only_accepts_ranges()
    {
        CleanupScanResult scan = Scan(
            (1, true, CleanupTier.Safe), (2, true, CleanupTier.Safe), (3, true, CleanupTier.Safe),
            (4, true, CleanupTier.Safe), (5, true, CleanupTier.Safe), (6, true, CleanupTier.Safe),
            (7, true, CleanupTier.Safe));

        CleanupCommandResult result = _parser.Parse("only 3-7", scan);

        Assert.NotNull(result.Plan);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, result.Plan!.Items.Select(item => item.Number));
    }

    [Fact]
    public void Keep_removes_the_listed_rows_from_the_plan()
    {
        CleanupScanResult scan = Scan((1, true, CleanupTier.Safe), (2, true, CleanupTier.Safe), (3, true, CleanupTier.Safe));

        CleanupCommandResult result = _parser.Parse("keep 1, 2", scan);

        Assert.NotNull(result.Plan);
        Assert.Equal(new[] { 3 }, result.Plan!.Items.Select(item => item.Number));
    }

    [Fact]
    public void Keep_does_not_add_unticked_opt_in_rows()
    {
        CleanupScanResult scan = Scan(
            (1, true, CleanupTier.Safe),
            (2, false, CleanupTier.OptIn),
            (3, true, CleanupTier.Safe));

        CleanupCommandResult result = _parser.Parse("keep 1", scan);

        Assert.NotNull(result.Plan);
        Assert.Equal(new[] { 3 }, result.Plan!.Items.Select(item => item.Number));
    }

    [Fact]
    public void Only_can_explicitly_choose_an_opt_in_row()
    {
        CleanupScanResult scan = Scan((1, true, CleanupTier.Safe), (2, false, CleanupTier.OptIn));

        CleanupCommandResult result = _parser.Parse("only 2", scan);

        Assert.NotNull(result.Plan);
        Assert.Equal(new[] { 2 }, result.Plan!.Items.Select(item => item.Number));
    }

    [Theory]
    [InlineData("remove 1 and 2")]
    [InlineData("proceed please")]
    [InlineData("only")]
    [InlineData("only 9")]
    [InlineData("only 0")]
    [InlineData("only 1, 1")]
    [InlineData("only 3-2")]
    [InlineData("only 1 and 2")]
    [InlineData("")]
    public void Unrecognised_or_invalid_commands_produce_no_plan(string command)
    {
        CleanupScanResult scan = Scan((1, true, CleanupTier.Safe), (2, true, CleanupTier.Safe));

        CleanupCommandResult result = _parser.Parse(command, scan);

        Assert.Equal(CleanupCommandKind.Clarification, result.Kind);
        Assert.Null(result.Plan);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Proceed_with_nothing_ticked_is_refused()
    {
        CleanupScanResult scan = Scan((1, false, CleanupTier.Caution), (2, false, CleanupTier.OptIn));

        CleanupCommandResult result = _parser.Parse("proceed", scan);

        Assert.Equal(CleanupCommandKind.Clarification, result.Kind);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Approval_message_states_count_and_size()
    {
        CleanupScanResult scan = Scan((1, true, CleanupTier.Safe), (2, true, CleanupTier.Safe));

        CleanupCommandResult result = _parser.Parse("proceed", scan);

        Assert.NotNull(result.Plan);
        Assert.Contains("2 item", result.Message);
    }

    private static CleanupScanResult Scan(params (int Number, bool Selected, CleanupTier Tier)[] rows)
    {
        var items = rows.Select(row => new CleanupCandidate
        {
            Number = row.Number,
            Rule = new CleanupRule
            {
                Id = $"test.{row.Number}",
                Category = "Test",
                Application = "Test",
                Name = $"Row {row.Number}",
                Description = "Test rule",
                SafetyNote = "Nothing else is touched.",
                RebuildImpact = "Rebuilt on demand.",
                Tier = row.Tier
            },
            Roots = new[] { $@"C:\Fake\Root{row.Number}" },
            Bytes = 1024,
            FileCount = 1,
            IsSelected = row.Selected
        }).ToList();

        return new CleanupScanResult { Items = items };
    }
}
