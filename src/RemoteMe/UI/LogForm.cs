using System.ComponentModel;
using System.Globalization;
using System.Text;
using RemoteMe.Core.Logging;

namespace RemoteMe.UI;

// Shows the in-memory log and follows new entries while it is open.
[DesignerCategory("Code")]
internal sealed class LogForm : Form {
    private readonly AppLog _log;
    private readonly ListView _entries;

    public LogForm(AppLog log) {
        _log = log;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 420);
        MinimumSize = new Size(400, 200);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "RemoteMe log";

        _entries = new ListView {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            AccessibleName = "Log entries",
        };
        _entries.Columns.Add("Time");
        _entries.Columns.Add("Level");
        _entries.Columns.Add("Source");
        _entries.Columns.Add("Message");

        var copy = new Button { Text = "Copy all", AutoSize = true, MinimumSize = new Size(96, 30) };
        var clear = new Button { Text = "Clear", AutoSize = true, MinimumSize = new Size(96, 30) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
        buttons.Controls.Add(copy);
        buttons.Controls.Add(clear);

        Controls.Add(_entries);
        Controls.Add(buttons);
        ResumeLayout(false);

        copy.Click += (_, _) => CopyAll();
        clear.Click += (_, _) => _log.Clear();
        _log.Added += OnAdded;
        _log.Cleared += OnCleared;
        foreach (var entry in _log.Entries) {
            _entries.Items.Add(ToItem(entry));
        }
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        FitColumns();
        ScrollToEnd();
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        FitColumns();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e) {
        base.OnDpiChanged(e);
        FitColumns();
    }

    // Column widths are pixels, so they follow the DPI here; the message takes the rest.
    private void FitColumns() {
        if (_entries is null || _entries.Columns.Count < 4) {
            return;
        }
        _entries.Columns[0].Width = LogicalToDeviceUnits(80);
        _entries.Columns[1].Width = LogicalToDeviceUnits(100);
        _entries.Columns[2].Width = LogicalToDeviceUnits(160);
        var used = _entries.Columns[0].Width + _entries.Columns[1].Width + _entries.Columns[2].Width;
        _entries.Columns[3].Width = Math.Max(LogicalToDeviceUnits(200), _entries.ClientSize.Width - used - SystemInformation.VerticalScrollBarWidth);
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        _log.Added -= OnAdded;
        _log.Cleared -= OnCleared;
        base.OnFormClosed(e);
    }

    private void OnAdded(object? sender, LogEntry entry) => RunOnUiThread(() => {
        _entries.Items.Add(ToItem(entry));
        ScrollToEnd();
    });

    private void OnCleared(object? sender, EventArgs e) => RunOnUiThread(() => _entries.Items.Clear());

    private void RunOnUiThread(Action action) {
        if (IsDisposed) {
            return;
        }
        if (InvokeRequired) {
            BeginInvoke(action);
        } else {
            action();
        }
    }

    private void ScrollToEnd() {
        if (_entries.Items.Count > 0) {
            _entries.EnsureVisible(_entries.Items.Count - 1);
        }
    }

    private void CopyAll() {
        var text = new StringBuilder();
        foreach (var entry in _log.Entries) {
            text.AppendLine(CultureInfo.InvariantCulture, $"{entry.Time:yyyy-MM-dd HH:mm:ss}\t{entry.Level}\t{entry.Source}\t{entry.Message}");
        }
        if (text.Length > 0) {
            Clipboard.SetText(text.ToString());
        }
    }

    private static ListViewItem ToItem(LogEntry entry) {
        var item = new ListViewItem(entry.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        item.SubItems.Add(entry.Level.ToString());
        item.SubItems.Add(entry.Source);
        item.SubItems.Add(entry.Message);
        if (entry.Level == LogLevel.Error) {
            item.ForeColor = DialogLayout.ErrorColor;
        } else if (entry.Level == LogLevel.Warning) {
            item.ForeColor = DialogLayout.WarningColor;
        }
        return item;
    }
}
