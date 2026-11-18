using RemoteMe.Core.Model;

namespace RemoteMe.Tests.Model;

public sealed class SettingsEditorTests {
    private static readonly Connection Alpha = SampleData.Connection("Alpha");
    private static readonly Connection Beta = SampleData.Connection("Beta", "Lab");
    private static readonly Connection Gamma = SampleData.Connection("Gamma", "Lab");
    private static readonly Connection Delta = SampleData.Connection("Delta", "Office");

    private static readonly Settings Sample = SettingsEditor.Normalize(new Settings {
        Connections = [Alpha, Beta, Gamma, Delta],
    });

    [Fact]
    public void Normalize_ListsGroupsInFirstUseOrderAndDropsUnusedOnes() {
        var settings = SettingsEditor.Normalize(new Settings {
            Groups = ["Old", "Office"],
            Connections = [Beta with { Group = "  Lab " }, Delta, Alpha with { Group = "   " }],
        });

        Assert.Equal(["Office", "Lab"], settings.Groups);
        Assert.Equal(["Lab", "Office", null], settings.Connections.Select(c => c.Group));
    }

    [Fact]
    public void AddConnection_PutsItLastInItsGroup() {
        var added = SampleData.Connection("Epsilon", "Lab");

        var settings = SettingsEditor.AddConnection(Sample, added);

        Assert.Equal(["Alpha", "Beta", "Gamma", "Epsilon", "Delta"], Names(settings));
    }

    [Fact]
    public void AddConnection_WithANewGroup_AddsTheGroupLast() {
        var settings = SettingsEditor.AddConnection(Sample, SampleData.Connection("Epsilon", "Home"));

        Assert.Equal(["Lab", "Office", "Home"], settings.Groups);
    }

    [Fact]
    public void UpdateConnection_InTheSameGroup_KeepsItsPlace() {
        var settings = SettingsEditor.UpdateConnection(Sample, Beta with { Name = "Beta 2" });

        Assert.Equal(["Alpha", "Beta 2", "Gamma", "Delta"], Names(settings));
    }

    [Fact]
    public void UpdateConnection_ToAnotherGroup_MovesItLastThereAndDropsTheEmptyGroup() {
        var settings = SettingsEditor.UpdateConnection(Sample, Delta with { Group = "Lab" });

        Assert.Equal(["Alpha", "Beta", "Gamma", "Delta"], Names(settings));
        Assert.Equal(["Lab"], settings.Groups);
    }

    [Fact]
    public void RemoveConnection_RemovesIt() {
        var settings = SettingsEditor.RemoveConnection(Sample, Gamma.Id);

        Assert.Equal(["Alpha", "Beta", "Delta"], Names(settings));
    }

    [Fact]
    public void MoveConnection_BeforeAnotherOne_TakesItsGroup() {
        var settings = SettingsEditor.MoveConnection(Sample, Alpha.Id, new ConnectionDrop("Lab", Gamma.Id, false));

        Assert.Equal(["Beta", "Alpha", "Gamma", "Delta"], Names(settings));
        Assert.Equal("Lab", settings.Connections[1].Group);
    }

    [Fact]
    public void MoveConnection_ToTheGroupStart_PutsItFirst() {
        var settings = SettingsEditor.MoveConnection(Sample, Gamma.Id, new ConnectionDrop("Lab", null, true));

        Assert.Equal(["Alpha", "Gamma", "Beta", "Delta"], Names(settings));
    }

    [Fact]
    public void MoveConnection_OutOfAGroup_ClearsTheGroup() {
        var settings = SettingsEditor.MoveConnection(Sample, Delta.Id, new ConnectionDrop(null, null, false));

        Assert.Null(settings.Connections.Single(c => c.Id == Delta.Id).Group);
        Assert.Equal(["Lab"], settings.Groups);
    }

    [Fact]
    public void MoveConnection_BeforeItself_ChangesNothing() {
        var settings = SettingsEditor.MoveConnection(Sample, Beta.Id, new ConnectionDrop("Lab", Beta.Id, false));

        Assert.Same(Sample, settings);
    }

    [Fact]
    public void MoveGroup_BeforeAnotherGroup_ReordersGroups() {
        var settings = SettingsEditor.MoveGroup(Sample, "Office", "Lab");

        Assert.Equal(["Office", "Lab"], settings.Groups);
    }

    [Fact]
    public void MoveGroup_ToTheEnd_PutsItLast() {
        var settings = SettingsEditor.MoveGroup(Sample, "Lab", null);

        Assert.Equal(["Office", "Lab"], settings.Groups);
    }

    [Fact]
    public void SetGroupCollapsed_CollapsesAndExpandsTheGroup() {
        var collapsed = SettingsEditor.SetGroupCollapsed(Sample, "Office", collapsed: true);
        var expanded = SettingsEditor.SetGroupCollapsed(collapsed, "Office", collapsed: false);

        Assert.Equal(["Office"], collapsed.CollapsedGroups);
        Assert.Empty(expanded.CollapsedGroups);
    }

    [Fact]
    public void Normalize_KeepsOnlyCollapsedGroupsInUseOnce() {
        var settings = SettingsEditor.Normalize(Sample with { CollapsedGroups = [" Lab ", "Lab", "Old", "  "] });

        Assert.Equal(["Lab"], settings.CollapsedGroups);
    }

    [Fact]
    public void UpdateConnection_ThatEmptiesACollapsedGroup_ForgetsTheGroup() {
        var settings = SettingsEditor.SetGroupCollapsed(Sample, "Office", collapsed: true);

        var moved = SettingsEditor.UpdateConnection(settings, Delta with { Group = "Lab" });

        Assert.Empty(moved.CollapsedGroups);
    }

    private static string[] Names(Settings settings) => settings.Connections.Select(c => c.Name).ToArray();
}
