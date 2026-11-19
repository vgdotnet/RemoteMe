using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RemoteMe.UI;

// "About RemoteMe" from the "..." menu (decided on 2026-11-19): the version with its commit, the notices
// the GPL asks an interactive program to show, links to the project, and the details a problem report
// needs, with buttons to copy them and to open the folder of the settings file.
[DesignerCategory("Code")]
internal sealed class AboutForm : Form {
    private const string Repository = "https://github.com/vgdotnet/RemoteMe";

    private readonly string _settingsPath;
    private readonly string _details;
    private readonly Bitmap _icon;

    public AboutForm(string settingsPath) {
        _settingsPath = settingsPath;
        _details = Details(settingsPath);

        SuspendLayout();
        DialogLayout.SetUp(this, "About RemoteMe");

        var grid = DialogLayout.CreateGrid(2);

        using (var large = new Icon(AppIcon.Value, 48, 48)) {
            _icon = large.ToBitmap();
        }
        var icon = new PictureBox {
            Image = _icon,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(48, 48),
            Margin = new Padding(3, 3, 12, 3),
        };
        var name = new Label { Text = "RemoteMe", AutoSize = true, Margin = new Padding(3, 3, 3, 0) };
        name.Font = new Font(name.Font.FontFamily, name.Font.Size * 1.4f, FontStyle.Bold);
        var heading = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        heading.Controls.Add(name);
        heading.Controls.Add(new Label { Text = "Version " + Version, AutoSize = true, Margin = new Padding(3, 2, 3, 0) });
        var description = DialogLayout.CreateText("A Remote Desktop client built on the Remote Desktop component of Windows.");
        description.Margin = new Padding(3, 3, 3, 3);
        heading.Controls.Add(description);
        // The address of the repository in full (decided on 2026-11-19).
        var address = CreateLink(Repository, Repository);
        address.Margin = new Padding(3, 0, 3, 9);
        heading.Controls.Add(address);
        grid.Controls.Add(icon, 0, 0);
        grid.Controls.Add(heading, 1, 0);
        grid.RowCount = 1;

        AddText(grid, "Copyright © 2026 Vitalii Guliaev");
        var license = new LinkLabel {
            Text = "RemoteMe comes with ABSOLUTELY NO WARRANTY. It is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License, version 3.",
            AutoSize = true,
            MaximumSize = new Size(DialogLayout.FieldWidth, 0),
            Margin = new Padding(3, 3, 3, 9),
        };
        const string licenseName = "GNU General Public License, version 3";
        license.LinkArea = new LinkArea(license.Text.IndexOf(licenseName, StringComparison.Ordinal), licenseName.Length);
        license.LinkClicked += (_, _) => Open(Repository + "/blob/main/LICENSE");
        grid.Controls.Add(license, 1, grid.RowCount++);

        var links = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        links.Controls.Add(CreateLink("Releases", Repository + "/releases"));
        links.Controls.Add(CreateLink("Report a problem", Repository + "/issues"));
        grid.Controls.Add(links, 1, grid.RowCount++);

        AddText(grid, "Uses the Remote Desktop component of Windows (mstscax.dll) and the Segoe MDL2 Assets icon font.");

        grid.Controls.Add(DialogLayout.CreateCaption("Details for a problem report"), 1, grid.RowCount++);
        var details = new TextBox {
            Text = _details,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Horizontal,
            Width = DialogLayout.FieldWidth,
            Height = 112,
            Margin = new Padding(3, 3, 3, 3),
            AccessibleName = "Details for a problem report",
        };
        grid.Controls.Add(details, 1, grid.RowCount++);

        var copy = new Button { Text = "Copy", AutoSize = true, MinimumSize = new Size(88, 0) };
        var folder = new Button { Text = "Open settings folder", AutoSize = true };
        var close = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(88, 0), DialogResult = DialogResult.OK };
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.Add(copy);
        buttons.Controls.Add(folder);
        var right = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        right.Controls.Add(close);
        var bar = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.Controls.Add(buttons, 0, 0);
        bar.Controls.Add(right, 1, 0);
        grid.Controls.Add(bar, 0, grid.RowCount);
        grid.SetColumnSpan(bar, 2);
        grid.RowCount++;
        AcceptButton = close;
        CancelButton = close;

        copy.Click += (_, _) => Clipboard.SetText(_details);
        folder.Click += (_, _) => Process.Start("explorer.exe", $"/select,\"{_settingsPath}\"");

        Controls.Add(grid);
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    // The release version and the commit the exe is built from.
    public static string Version {
        get {
            var assembly = typeof(AboutForm).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var revision = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(m => m.Key == "SourceRevision")?.Value;
            return Core.About.ProductVersion.Format(version, revision);
        }
    }

    private static string Details(string settingsPath) {
        var windows = RuntimeInformation.OSDescription;
        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", null) is string release) {
            windows += $" ({release})";
        }
        var rdp = Path.Combine(Environment.SystemDirectory, "mstscax.dll");
        // The version text, such as 10.0.26100.8875, without the build branch after it; the number fields
        // of the file still say 6.2 for old programs.
        var rdpVersion = File.Exists(rdp) ? FileVersionInfo.GetVersionInfo(rdp).FileVersion?.Split(' ')[0] ?? "unknown" : "not found";
        return string.Join(Environment.NewLine,
            "RemoteMe " + Version,
            "Windows: " + windows,
            "Remote Desktop component: " + rdpVersion,
            "Runtime: " + RuntimeInformation.FrameworkDescription,
            "Settings file: " + settingsPath);
    }

    private static void AddText(TableLayoutPanel grid, string text) => grid.Controls.Add(DialogLayout.CreateText(text), 1, grid.RowCount++);

    private static LinkLabel CreateLink(string text, string url) {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(3, 3, 12, 3) };
        link.LinkClicked += (_, _) => Open(url);
        return link;
    }

    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
