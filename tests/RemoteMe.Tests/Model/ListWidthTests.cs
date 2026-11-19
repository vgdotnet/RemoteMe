using RemoteMe.Core.Model;

namespace RemoteMe.Tests.Model;

public sealed class ListWidthTests {
    private const int Full = 200;
    private const int Compact = 44;

    [Fact]
    public void AfterHandleClick_AWideList_GoesToTheClockWidth() {
        Assert.Equal(Full, ListWidth.AfterHandleClick(450, Full, Compact));
    }

    [Fact]
    public void AfterHandleClick_TheClockWidth_GoesToTheCompactStrip() {
        Assert.Equal(Compact, ListWidth.AfterHandleClick(Full, Full, Compact));
    }

    [Fact]
    public void AfterHandleClick_TheCompactStrip_GoesBackToTheClockWidth() {
        Assert.Equal(Full, ListWidth.AfterHandleClick(Compact, Full, Compact));
    }
}
