namespace RemoteMe.Core.Model;

// Where a click on the handle at the edge of the list takes its width (decided on 2026-11-19): a list
// wider than the clock goes to the clock's width, the clock's width to the compact strip, and the strip
// back to the clock's width, not to the width the list had before.
public static class ListWidth {
    public static int AfterHandleClick(int width, int full, int compact) => width == full ? compact : full;
}
