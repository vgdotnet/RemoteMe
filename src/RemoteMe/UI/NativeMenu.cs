using static RemoteMe.NativeMethods;

namespace RemoteMe.UI;

// A menu that Windows draws itself, like the menus of other programs: their size and spacing, and no
// gray strip on the left for images (decided on 2026-11-19, instead of ContextMenuStrip). The menu is
// built each time it opens, so the states of its items are always current, and the chosen command runs
// after the menu has closed.
internal sealed class NativeMenu {
    private readonly List<Item> _items = [];

    private sealed record Item(string? Text, Action? Click, bool Enabled, bool Checked, NativeMenu? Submenu);

    // The shortcut is shown at the right edge of the menu, as Windows does it for the text after a tab.
    public NativeMenu Add(string text, Action click, bool enabled = true, bool isChecked = false, string? shortcut = null) {
        _items.Add(new Item(shortcut is null ? text : text + "\t" + shortcut, click, enabled, isChecked, null));
        return this;
    }

    public NativeMenu AddSubmenu(string text, NativeMenu submenu) {
        _items.Add(new Item(text, null, true, false, submenu));
        return this;
    }

    public NativeMenu AddSeparator() {
        _items.Add(new Item(null, null, true, false, null));
        return this;
    }

    // Opens the menu at a point on the screen, by default with its top left corner there. Exclude is a
    // part of the screen the menu should not cover, such as the button that opened it.
    public void Show(Control owner, Point location, uint alignment = TpmLeftAlign | TpmTopAlign, Rectangle exclude = default) {
        var commands = new List<Action>();
        var menu = Build(commands);
        int command;
        try {
            var parameters = new TpmParams {
                Size = System.Runtime.InteropServices.Marshal.SizeOf<TpmParams>(),
                Exclude = new Rect { Left = exclude.Left, Top = exclude.Top, Right = exclude.Right, Bottom = exclude.Bottom },
            };
            command = TrackPopupMenuEx(menu, alignment | TpmVertical | TpmRightButton | TpmReturnCmd, location.X, location.Y, owner.Handle, ref parameters);
        } finally {
            // Destroys the submenus too.
            DestroyMenu(menu);
        }
        if (command > 0) {
            commands[command - 1]();
        }
    }

    private IntPtr Build(List<Action> commands) {
        var menu = CreatePopupMenu();
        foreach (var item in _items) {
            if (item.Text is null) {
                AppendMenu(menu, MfSeparator, UIntPtr.Zero, null);
            } else if (item.Submenu is { } submenu) {
                AppendMenu(menu, MfPopup, (UIntPtr)(nuint)submenu.Build(commands), item.Text);
            } else {
                commands.Add(item.Click!);
                var flags = MfString | (item.Enabled ? 0 : MfGrayed) | (item.Checked ? MfChecked : 0);
                AppendMenu(menu, flags, (UIntPtr)(uint)commands.Count, item.Text);
            }
        }
        return menu;
    }
}
