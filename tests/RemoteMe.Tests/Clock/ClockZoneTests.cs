using RemoteMe.Core.Clock;

namespace RemoteMe.Tests.Clock;

public sealed class ClockZoneTests {
    private static readonly TimeZoneInfo India = TimeZoneInfo.CreateCustomTimeZone(
        "Test India", new TimeSpan(5, 30, 0), "(UTC+05:30) Chennai, Kolkata, Mumbai, New Delhi", "India Standard Time");

    [Theory]
    [InlineData(7, 0, "UTC+7")]
    [InlineData(-3, -30, "UTC-3:30")]
    [InlineData(5, 45, "UTC+5:45")]
    [InlineData(0, 0, "UTC+0")]
    public void FormatOffset_ShowsMinutesOnlyWhenThereAreAny(int hours, int minutes, string expected) {
        Assert.Equal(expected, ClockZone.FormatOffset(new TimeSpan(hours, minutes, 0)));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("  Kolkata ", true)]
    [InlineData("mumbai", true)]
    [InlineData("Paris", false)]
    [InlineData("+5:30", true)]
    [InlineData("5:30", true)]
    [InlineData("UTC+0530", true)]
    [InlineData("+5", false)]
    [InlineData("-5:30", false)]
    public void Matches_ACityOrAUtcOffset(string query, bool expected) {
        Assert.Equal(expected, ClockZone.Matches(India, query));
    }

    [Fact]
    public void Matches_TheOffsetInTheZoneNameToo() {
        // Windows names British Columbia by UTC-07:00, while its standard offset stays -8:00.
        var zone = TimeZoneInfo.CreateCustomTimeZone(
            "Test BC", TimeSpan.FromHours(-8), "(UTC-07:00) Pacific Time (British Columbia)", "Pacific Standard Time");

        Assert.True(ClockZone.Matches(zone, "-7"));
        Assert.True(ClockZone.Matches(zone, "-8"));
        Assert.False(ClockZone.Matches(zone, "+7"));
    }

    [Fact]
    public void Matches_TheOffsetOfTheMomentToo() {
        // Like Madrid: UTC+1, and UTC+2 from the last Sunday of March to the last Sunday of October.
        var summerTime = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));
        var zone = TimeZoneInfo.CreateCustomTimeZone(
            "Test Madrid", TimeSpan.FromHours(1), "(UTC+01:00) Brussels, Copenhagen, Madrid, Paris",
            "Romance Standard Time", "Romance Daylight Time", [summerTime]);
        var summer = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var winter = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.True(ClockZone.Matches(zone, "+2", summer));
        Assert.True(ClockZone.Matches(zone, "+1", summer));
        Assert.False(ClockZone.Matches(zone, "+2", winter));
        Assert.True(ClockZone.Matches(zone, "+1", winter));
    }

    [Theory]
    [InlineData("GMT Standard Time", "London")]
    [InlineData("N. Central Asia Standard Time", "Novosibirsk")]
    [InlineData("Argentina Standard Time", "Buenos Aires")]
    [InlineData("UTC-02", "UTC")]
    public void CityName_IsTheMainCityOfTheZone(string id, string expected) {
        Assert.Equal(expected, ClockZone.CityName(TimeZoneInfo.FindSystemTimeZoneById(id)));
    }

    [Fact]
    public void Find_GivesNullForTheLocalTimeAndForAnUnknownId() {
        Assert.Null(ClockZone.Find(null));
        Assert.Null(ClockZone.Find("No Such Standard Time"));
        Assert.Equal("Tokyo Standard Time", ClockZone.Find("Tokyo Standard Time")?.Id);
    }
}
