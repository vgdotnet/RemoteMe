using System.ComponentModel;
using RemoteMe.Core.Clock;

namespace RemoteMe.UI;

// Chooses the time zone of the clock: a search box over the list of Windows time zones, with the local
// time first.
[DesignerCategory("Code")]
internal sealed class TimeZoneForm : Form {
    private const int ListWidth = 440;

    private readonly IReadOnlyList<TimeZoneInfo> _zones = TimeZoneInfo.GetSystemTimeZones();
    private readonly TextBox _search;
    private readonly ListBox _list;
    private readonly ZoneItem _local = new(null, $"{ClockPanel.LocalName}: {TimeZoneInfo.Local.DisplayName}");

    public TimeZoneForm(string? zoneId) {
        SuspendLayout();
        DialogLayout.SetUp(this, "Clock time zone");
        var grid = DialogLayout.CreateGrid(1);
        grid.Controls.Add(DialogLayout.CreateCaption("Show the time of"));
        _search = new TextBox {
            Width = ListWidth,
            PlaceholderText = "City or UTC offset, such as +7",
            AccessibleName = "Search time zones",
        };
        grid.Controls.Add(_search);
        _list = new ListBox {
            Width = ListWidth,
            Height = 240,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            AccessibleName = "Time zones",
        };
        grid.Controls.Add(_list);
        grid.Controls.Add(DialogLayout.CreateButtons(this, "OK", "Cancel", out var accept));
        Controls.Add(grid);
        ResumeLayout(false);
        PerformLayout();

        ZoneId = ClockZone.Find(zoneId)?.Id;
        ShowZones();
        ActiveControl = _search;
        _search.TextChanged += (_, _) => ShowZones();
        _search.KeyDown += (_, e) => MoveSelection(e);
        _list.DoubleClick += (_, _) => Accept();
        accept.Click += (_, _) => Accept();
    }

    // Null is the local time.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ZoneId { get; private set; }

    // Keeps the selected zone selected while it matches the search, otherwise selects the first one.
    private void ShowZones() {
        var keepId = _list.SelectedItem is ZoneItem current ? current.Id : ZoneId;
        var query = _search.Text;
        var items = new List<ZoneItem>();
        // The local line is found like its zone, by a city or an offset, and also by its own caption.
        if (ClockZone.Matches(TimeZoneInfo.Local, query)
            || ClockPanel.LocalName.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase)) {
            items.Add(_local);
        }
        items.AddRange(_zones.Where(z => ClockZone.Matches(z, query)).Select(z => new ZoneItem(z.Id, z.DisplayName)));
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Items.AddRange([.. items]);
        var selected = items.FindIndex(i => i.Id == keepId);
        _list.SelectedIndex = selected >= 0 ? selected : items.Count > 0 ? 0 : -1;
        _list.EndUpdate();
    }

    // The arrow and page keys in the search box move through the list.
    private void MoveSelection(KeyEventArgs e) {
        var step = e.KeyCode switch {
            Keys.Down => 1,
            Keys.Up => -1,
            Keys.PageDown => Math.Max(1, _list.ClientSize.Height / _list.ItemHeight - 1),
            Keys.PageUp => -Math.Max(1, _list.ClientSize.Height / _list.ItemHeight - 1),
            _ => 0,
        };
        if (step == 0 || _list.Items.Count == 0) {
            return;
        }
        e.Handled = true;
        _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + step, 0, _list.Items.Count - 1);
    }

    private void Accept() {
        if (_list.SelectedItem is not ZoneItem item) {
            return;
        }
        ZoneId = item.Id;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record ZoneItem(string? Id, string Text) {
        public override string ToString() => Text;
    }
}
