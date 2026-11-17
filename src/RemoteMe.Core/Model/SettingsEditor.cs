namespace RemoteMe.Core.Model;

// Pure edits of the settings. Every result is normalized: group names are trimmed, every group in use
// is listed in Settings.Groups, and groups without connections are dropped.
public static class SettingsEditor {
    public static Settings Normalize(Settings settings) {
        var connections = settings.Connections.Select(c => c with { Group = CleanGroup(c.Group) }).ToList();
        var used = connections.Select(c => c.Group).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var groups = settings.Groups
            .Select(CleanGroup)
            .OfType<string>()
            .Where(used.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var group in connections.Select(c => c.Group).OfType<string>()) {
            if (!groups.Contains(group, StringComparer.Ordinal)) {
                groups.Add(group);
            }
        }
        return settings with { Groups = groups, Connections = connections };
    }

    public static string? CleanGroup(string? group) => string.IsNullOrWhiteSpace(group) ? null : group.Trim();

    public static Settings AddConnection(Settings settings, Connection connection) {
        var connections = settings.Connections.ToList();
        Insert(connections, connection with { Group = CleanGroup(connection.Group) }, beforeId: null, atGroupStart: false);
        return Normalize(settings with { Connections = connections });
    }

    public static Settings UpdateConnection(Settings settings, Connection connection) {
        var connections = settings.Connections.ToList();
        var index = IndexOf(connections, connection.Id);
        var updated = connection with { Group = CleanGroup(connection.Group) };
        if (CleanGroup(connections[index].Group) == updated.Group) {
            connections[index] = updated;
        } else {
            connections.RemoveAt(index);
            Insert(connections, updated, beforeId: null, atGroupStart: false);
        }
        return Normalize(settings with { Connections = connections });
    }

    public static Settings RemoveConnection(Settings settings, Guid id) {
        var connections = settings.Connections.ToList();
        connections.RemoveAt(IndexOf(connections, id));
        return Normalize(settings with { Connections = connections });
    }

    public static Settings MoveConnection(Settings settings, Guid id, ConnectionDrop drop) {
        if (drop.BeforeId == id) {
            return settings;
        }
        var connections = settings.Connections.ToList();
        var index = IndexOf(connections, id);
        var moved = connections[index] with { Group = CleanGroup(drop.Group) };
        connections.RemoveAt(index);
        Insert(connections, moved, drop.BeforeId, drop.AtGroupStart);
        return Normalize(settings with { Connections = connections });
    }

    public static Settings MoveGroup(Settings settings, string group, string? beforeGroup) {
        if (group == beforeGroup) {
            return settings;
        }
        var groups = settings.Groups.ToList();
        if (!groups.Remove(group)) {
            throw new ArgumentException($"There is no group named \"{group}\".", nameof(group));
        }
        var index = beforeGroup is null ? -1 : groups.IndexOf(beforeGroup);
        groups.Insert(index >= 0 ? index : groups.Count, group);
        return Normalize(settings with { Groups = groups });
    }

    private static void Insert(List<Connection> connections, Connection connection, Guid? beforeId, bool atGroupStart) {
        var index = -1;
        if (beforeId is { } id) {
            index = connections.FindIndex(c => c.Id == id && c.Group == connection.Group);
        }
        if (index < 0 && atGroupStart) {
            index = connections.FindIndex(c => c.Group == connection.Group);
        }
        if (index < 0) {
            var last = connections.FindLastIndex(c => c.Group == connection.Group);
            index = last >= 0 ? last + 1 : connections.Count;
        }
        connections.Insert(index, connection);
    }

    private static int IndexOf(List<Connection> connections, Guid id) {
        var index = connections.FindIndex(c => c.Id == id);
        return index >= 0 ? index : throw new ArgumentException($"There is no connection with id {id}.", nameof(id));
    }
}
