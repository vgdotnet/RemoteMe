namespace RemoteMe.Core.Model;

public abstract record ListRow;

public sealed record GroupRow(string Name, int ConnectionCount, bool IsCollapsed) : ListRow;

public sealed record ConnectionRow(Connection Connection) : ListRow;

// Where a dragged connection lands: in Group, before BeforeId, or first in the group, or else last.
public sealed record ConnectionDrop(string? Group, Guid? BeforeId, bool AtGroupStart);

// The rows of the connection list: connections without a group first, then each group with a
// caption. Drops are given as a row index (Count for below the last row) and the half of that row.
public static class ConnectionListLayout {
    public static IReadOnlyList<ListRow> Build(Settings settings, IReadOnlySet<string> collapsedGroups) {
        var rows = new List<ListRow>();
        rows.AddRange(settings.Connections.Where(c => c.Group is null).Select(c => new ConnectionRow(c)));
        foreach (var group in settings.Groups) {
            var members = settings.Connections.Where(c => c.Group == group).ToList();
            var collapsed = collapsedGroups.Contains(group);
            rows.Add(new GroupRow(group, members.Count, collapsed));
            if (!collapsed) {
                rows.AddRange(members.Select(c => new ConnectionRow(c)));
            }
        }
        return rows;
    }

    // The connections in list order, collapsed groups included.
    public static IReadOnlyList<Guid> ConnectionOrder(Settings settings) => Build(settings, new HashSet<string>())
        .OfType<ConnectionRow>()
        .Select(row => row.Connection.Id)
        .ToList();

    public static ConnectionDrop DropConnection(IReadOnlyList<ListRow> rows, int index, bool upperHalf) {
        if (rows.Count == 0) {
            return new ConnectionDrop(null, null, false);
        }
        if (index >= rows.Count) {
            return new ConnectionDrop(SectionGroup(rows[^1]), null, false);
        }
        switch (rows[index]) {
            case ConnectionRow row when upperHalf:
                return new ConnectionDrop(row.Connection.Group, row.Connection.Id, false);
            case ConnectionRow row:
                var next = index + 1 < rows.Count ? rows[index + 1] as ConnectionRow : null;
                return next is not null && next.Connection.Group == row.Connection.Group
                    ? new ConnectionDrop(row.Connection.Group, next.Connection.Id, false)
                    : new ConnectionDrop(row.Connection.Group, null, false);
            case GroupRow when upperHalf:
                return new ConnectionDrop(index == 0 ? null : SectionGroup(rows[index - 1]), null, false);
            case GroupRow group:
                return new ConnectionDrop(group.Name, null, true);
            default:
                throw new ArgumentException("Unknown row type.", nameof(rows));
        }
    }

    // Returns the group the dragged group goes before, or null for the end.
    public static string? DropGroup(IReadOnlyList<ListRow> rows, int index, bool upperHalf) {
        if (index >= rows.Count) {
            return null;
        }
        if (rows[index] is GroupRow header && upperHalf) {
            return header.Name;
        }
        if (SectionGroup(rows[index]) is null) {
            return rows.OfType<GroupRow>().FirstOrDefault()?.Name;
        }
        for (var i = index + 1; i < rows.Count; i++) {
            if (rows[i] is GroupRow following) {
                return following.Name;
            }
        }
        return null;
    }

    private static string? SectionGroup(ListRow row) => row switch {
        GroupRow group => group.Name,
        ConnectionRow connection => connection.Connection.Group,
        _ => throw new ArgumentException("Unknown row type.", nameof(row)),
    };
}
