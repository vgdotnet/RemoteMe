using RemoteMe.Core.Logging;

namespace RemoteMe.Tests.Logging;

public sealed class HangDetectorTests {
    private static readonly TimeSpan Threshold = TimeSpan.FromSeconds(5);

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Check_WhileTheWindowAnswers_ReportsNothing() {
        var detector = new HangDetector(Threshold, S(0));

        for (var second = 1; second <= 20; second++) {
            Assert.Null(detector.Check(S(second)));
            detector.Answered(S(second + 0.01));
        }
    }

    [Fact]
    public void Check_AfterTheThresholdOfSilence_ReportsTheHangOnce() {
        var detector = new HangDetector(Threshold, S(0));

        for (var second = 1; second <= 4; second++) {
            Assert.Null(detector.Check(S(second)));
        }
        Assert.Equal(new HangChange(true, S(5)), detector.Check(S(5)));
        Assert.Null(detector.Check(S(6)));
        Assert.Null(detector.Check(S(7)));
    }

    [Fact]
    public void Check_WhenTheWindowAnswersAgain_ReportsHowLongItWasSilent() {
        var detector = new HangDetector(Threshold, S(10));
        for (var second = 11; second <= 30; second++) {
            detector.Check(S(second));
        }

        detector.Answered(S(30.5));

        Assert.Equal(new HangChange(false, S(20.5)), detector.Check(S(31)));
        Assert.Null(detector.Check(S(32)));
    }

    [Fact]
    public void Check_AfterTheWatchdogItselfStoodStill_DoesNotCountThatSilence() {
        var detector = new HangDetector(Threshold, S(0));
        Assert.Null(detector.Check(S(1)));

        // The computer slept between these two checks.
        Assert.Null(detector.Check(S(3600)));
        detector.Answered(S(3600.01));

        Assert.Null(detector.Check(S(3601)));
    }

    [Fact]
    public void Check_AHangAfterAPause_IsStillReported() {
        var detector = new HangDetector(Threshold, S(0));
        detector.Check(S(1));
        detector.Check(S(3600));

        for (var second = 3601; second < 3605; second++) {
            Assert.Null(detector.Check(S(second)));
        }
        Assert.Equal(new HangChange(true, S(5)), detector.Check(S(3605)));
    }
}
