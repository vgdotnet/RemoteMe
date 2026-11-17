namespace RemoteMe.UI;

internal enum ConflictChoice {
    Reload,
    Overwrite,
    Cancel,
}

internal static class Messages {
    public const string Title = "RemoteMe";

    public static void Error(IWin32Window? owner, string text) {
        MessageBox.Show(owner, text, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static void Information(IWin32Window owner, string text) {
        MessageBox.Show(owner, text, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool Confirm(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            == DialogResult.Yes;

    public static ConflictChoice AskConflict(IWin32Window owner) {
        var reload = new TaskDialogButton("Reload");
        var overwrite = new TaskDialogButton("Overwrite");
        var page = new TaskDialogPage {
            Caption = Title,
            Heading = "The settings file was changed outside RemoteMe",
            Text = "Reload reads the file again; the change you just made is lost.\n"
                + "Overwrite replaces the file with the settings shown here; the changes made outside are lost.",
            Icon = TaskDialogIcon.Warning,
            Buttons = { reload, overwrite, TaskDialogButton.Cancel },
            DefaultButton = reload,
        };
        var result = TaskDialog.ShowDialog(owner, page);
        return result == reload ? ConflictChoice.Reload
            : result == overwrite ? ConflictChoice.Overwrite
            : ConflictChoice.Cancel;
    }
}
