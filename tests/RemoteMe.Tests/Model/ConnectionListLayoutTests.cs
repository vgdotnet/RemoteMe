using RemoteMe.Core.Model;

namespace RemoteMe.Tests.Model;

public sealed class ConnectionListLayoutTests {
    private static readonly Connection Alpha = SampleData.Connection("Alpha");
    private static readonly Connection Beta = SampleData.Connection("Beta", "Lab");
    private static readonly Connection Gamma = SampleData.Connection("Gamma", "Lab");
    private static readonly Connection Delta = SampleData.Connection("Delta", "Office");

    private static readonly Settings Sample = SettingsEditor.Normalize(new Settings {
        Groups = ["Office", "Lab"],
        Connections = [Beta, Alpha, Gamma, Delta],
    });

    // Rows of the sample: 0 Alpha, 1 [Office], 2 Delta, 3 [Lab], 4 Beta, 5 Gamma.
    private static readonly IReadOnlyList<ListRow> Rows = ConnectionListLayout.Build(Sample, new HashSet<string>());

    [Fact]
    public void Build_WithoutGroups_GivesAFlatList() {
        var settings = new Settings { Connections = [Alpha, Alpha with { Id = Guid.NewGuid(), Name = "Second" }] };

        var rows = ConnectionListLayout.Build(settings, new HashSet<string>());

        Assert.All(rows, row => Assert.IsType<ConnectionRow>(row));
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Build_PutsConnectionsWithoutAGroupFirstThenGroupsInTheirOrder() {
        Assert.Equal(["Alpha", "[Office]", "Delta", "[Lab]", "Beta", "Gamma"], Describe(Rows));
    }

    [Fact]
    public void Build_WithACollapsedGroup_ShowsOnlyItsCaption() {
        var rows = ConnectionListLayout.Build(Sample, new HashSet<string> { "Lab" });

        Assert.Equal(["Alpha", "[Office]", "Delta", "[Lab]"], Describe(rows));
        Assert.Equal(new GroupRow("Lab", 2, true), rows[^1]);
    }

    [Fact]
    public void BuildCompact_KeepsTheConnectionsAndMarksWhereEachGroupStarts() {
        var (rows, starts) = ConnectionListLayout.BuildCompact(Sample, new HashSet<string>());

        Assert.Equal(["Alpha", "Delta", "Beta", "Gamma"], Describe(rows));
        Assert.Equal([1, 2], starts.Order());
    }

    [Fact]
    public void BuildCompact_LeavesOutCollapsedGroups() {
        var (rows, starts) = ConnectionListLayout.BuildCompact(Sample, new HashSet<string> { "Office" });

        Assert.Equal(["Alpha", "Beta", "Gamma"], Describe(rows));
        Assert.Equal([1], starts);
    }

    [Fact]
    public void BuildCompact_DoesNotMarkTheFirstGroupWhenNothingIsAboveIt() {
        var settings = SettingsEditor.Normalize(new Settings { Connections = [Beta, Delta] });

        var (rows, starts) = ConnectionListLayout.BuildCompact(settings, new HashSet<string>());

        Assert.Equal(["Beta", "Delta"], Describe(rows));
        Assert.Equal([1], starts);
    }

    [Theory]
    [InlineData(4, true, "Lab", "Beta", false)]
    [InlineData(4, false, "Lab", "Gamma", false)]
    [InlineData(5, false, "Lab", null, false)]
    [InlineData(3, false, "Lab", null, true)]
    [InlineData(3, true, "Office", null, false)]
    [InlineData(1, true, null, null, false)]
    [InlineData(6, false, "Lab", null, false)]
    public void DropConnection_FollowsThePointedHalf(int index, bool upperHalf, string? group, string? before, bool atStart) {
        var drop = ConnectionListLayout.DropConnection(Rows, index, upperHalf);

        Assert.Equal(group, drop.Group);
        Assert.Equal(before is null ? null : Sample.Connections.Single(c => c.Name == before).Id, drop.BeforeId);
        Assert.Equal(atStart, drop.AtGroupStart);
    }

    [Theory]
    [InlineData(3, true, "Lab")]
    [InlineData(1, false, "Lab")]
    [InlineData(2, true, "Lab")]
    [InlineData(5, false, null)]
    [InlineData(0, false, "Office")]
    [InlineData(6, true, null)]
    public void DropGroup_GoesBeforeThePointedGroupOrTheNextOne(int index, bool upperHalf, string? expected) {
        Assert.Equal(expected, ConnectionListLayout.DropGroup(Rows, index, upperHalf));
    }

    private static string[] Describe(IReadOnlyList<ListRow> rows) => rows
        .Select(row => row switch {
            GroupRow group => $"[{group.Name}]",
            ConnectionRow connection => connection.Connection.Name,
            _ => "?",
        })
        .ToArray();
}
