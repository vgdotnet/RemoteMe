namespace RemoteMe.Core.Model;

// The color of the doodle background of the left panel - below the connection rows, behind the clock and
// the list buttons, and behind the circles of the compact strip - chosen in the "..." menu (decided on
// 2026-11-19). The session area stays plain gray. None, the last name, is the plain panel without
// doodles, as it was before them.
public static class ListBackground {
    public const string None = "None";

    public static IReadOnlyList<string> Names { get; } = ["Green", "Blue", "Peach", "Lavender", "Mint", "Sand", None];

    public static string Of(Settings settings) =>
        settings.Background is { } chosen && Names.Contains(chosen) ? chosen : Names[0];
}
