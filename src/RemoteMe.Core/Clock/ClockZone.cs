using System.Globalization;
using System.Text.RegularExpressions;

namespace RemoteMe.Core.Clock;

// The time zone of the clock above the list buttons, kept in Settings.ClockTimeZone.
public static partial class ClockZone {
    // Null for the local time, and for an id this computer does not know.
    public static TimeZoneInfo? Find(string? id) {
        if (id is null) {
            return null;
        }
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        } catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) {
            return null;
        }
    }

    // The zone's main city from its IANA name (Europe/London - London), since Windows names a zone by
    // several cities. Zones without a city, such as UTC-02, are "UTC".
    public static string CityName(TimeZoneInfo zone) {
        var iana = zone.HasIanaId ? zone.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var converted) ? converted : null;
        if (iana is null) {
            return OffsetPrefix().Replace(zone.DisplayName, "");
        }
        if (iana.StartsWith("Etc/", StringComparison.Ordinal) || !iana.Contains('/', StringComparison.Ordinal)) {
            return "UTC";
        }
        return iana[(iana.LastIndexOf('/') + 1)..].Replace('_', ' ');
    }

    // UTC+7, UTC-3:30, UTC+0.
    public static string FormatOffset(TimeSpan offset) {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var value = offset.Duration();
        var hours = (int)value.TotalHours;
        return value.Minutes == 0
            ? string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{hours}")
            : string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{hours}:{value.Minutes:00}");
    }

    public static bool Matches(TimeZoneInfo zone, string query) => Matches(zone, query, DateTimeOffset.UtcNow);

    // A query is a word of the zone's name or city, or a UTC offset such as +7, -3:30 or UTC+5:45. An
    // offset is compared with the zone's offset at the given moment, which the clock shows, with its
    // standard offset and with the one its name shows, since Windows names some zones by an offset they
    // no longer have in winter.
    public static bool Matches(TimeZoneInfo zone, string query, DateTimeOffset now) {
        query = query.Trim();
        if (query.Length == 0) {
            return true;
        }
        if (ParseOffset(OffsetQuery().Match(query)) is { } offset) {
            return zone.GetUtcOffset(now) == offset
                || zone.BaseUtcOffset == offset
                || ParseOffset(NamedOffset().Match(zone.DisplayName)) == offset;
        }
        return zone.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || CityName(zone).Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private static TimeSpan? ParseOffset(Match match) {
        if (!match.Success) {
            return null;
        }
        var hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture) : 0;
        var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture) : 0;
        var offset = new TimeSpan(hours, minutes, 0);
        return match.Groups["sign"].Value == "-" ? -offset : offset;
    }

    [GeneratedRegex(@"^\(UTC[^)]*\)\s*")]
    private static partial Regex OffsetPrefix();

    // (UTC+05:30) Chennai, (UTC) Coordinated Universal Time.
    [GeneratedRegex(@"^\(UTC(?:(?<sign>[+-])(?<hours>\d{2}):(?<minutes>\d{2}))?\)")]
    private static partial Regex NamedOffset();

    [GeneratedRegex(@"^(?:(?:UTC|GMT)\s*)?(?<sign>[+-])?\s*(?<hours>\d{1,2})(?::?(?<minutes>\d{2}))?$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetQuery();
}
