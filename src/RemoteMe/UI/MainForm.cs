using System.ComponentModel;
using System.Runtime.InteropServices;
using RemoteMe.Core.Logging;
using RemoteMe.Core.Model;
using RemoteMe.Core.Sessions;
using RemoteMe.Core.Vault;
using RemoteMe.Input;
using RemoteMe.RdpInterop;

namespace RemoteMe.UI;

[DesignerCategory("Code")]
internal sealed class MainForm : Form {
    private const string LogSource = "Settings";
    private const string ReturnText = "Return to main window";
    private const string AddGlyph = "";
    private const string MoreGlyph = "";
    private const string FullScreenGlyph = "";
    private const string WindowGlyph = "";

    private readonly SettingsStore _store;
    private readonly AppLog _log;
    private readonly string _logPath;
    private readonly SessionManager _sessions;
    private readonly Dictionary<Guid, SessionWindow> _windows = [];
    private readonly Dictionary<Guid, FullScreenWindow> _fullScreens = [];
    private readonly SplitContainer _split;
    private readonly Button _listStrip;
    private readonly ToolTip _toolTip = new();
    private readonly Font _stripFont;
    private readonly GlyphButton _fullScreenButton;
    private readonly GlyphButton _addButton;
    private readonly GlyphButton _moreButton;
    private readonly TableLayoutPanel _bottomBar;
    private readonly WallpaperPanel _leftPanel;
    private readonly List<Button> _iconButtons = [];
    private readonly Panel _bottomLine;
    private readonly Font _glyphFont;
    private readonly ConnectionList _list;
    private readonly ClockPanel _clock;
    private readonly SessionView _mainView;
    private HotkeyHook? _hotkeys;
    private Guid? _menuConnectionId;
    private LogForm? _logForm;
    private bool _mainFullScreen;
    private Rectangle _windowedBounds;
    private bool _windowedMaximized;
    private bool _closing;

    public MainForm(SettingsStore store, AppLog log, string logPath) {
        _store = store;
        _log = log;
        _logPath = logPath;
        _sessions = new SessionManager(CreateRdpClient, log);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Text = Messages.Title;
        Icon = AppIcon.Value;

        _list = new ConnectionList { Dock = DockStyle.Fill };
        _glyphFont = new Font("Segoe MDL2 Assets", 11f);
        _addButton = CreateIconButton(AddGlyph, "Add connection");
        _fullScreenButton = CreateIconButton(FullScreenGlyph, "");
        ShowMainFullScreenState();
        _moreButton = CreateIconButton(MoreGlyph, "More commands");
        _bottomBar = new TableLayoutPanel {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Padding = Padding.Empty,
            BackColor = Color.Transparent,
        };
        _bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottomBar.Controls.Add(_addButton, 0, 0);
        _bottomBar.Controls.Add(_fullScreenButton, 2, 0);
        _bottomBar.Controls.Add(_moreButton, 3, 0);
        _bottomLine = new Panel { Dock = DockStyle.Bottom, Height = 1 };
        ApplyPalette();
        _clock = new ClockPanel { Dock = DockStyle.Bottom };

        _mainView = new SessionView();
        _split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        _leftPanel = new WallpaperPanel { Dock = DockStyle.Fill };
        _leftPanel.Controls.Add(_list);
        _leftPanel.Controls.Add(_bottomLine);
        _leftPanel.Controls.Add(_clock);
        _leftPanel.Controls.Add(_bottomBar);
        _split.Panel1.Controls.Add(_leftPanel);
        _split.Panel2.Controls.Add(_mainView);
        _listStrip = new Button {
            Dock = DockStyle.Left,
            Width = 18,
            Text = "›",
            Visible = false,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            BackColor = SystemColors.ControlDark,
            ForeColor = SystemColors.ControlLightLight,
            AccessibleName = "Show the list",
        };
        _listStrip.Font = _stripFont = new Font(_listStrip.Font.FontFamily, _listStrip.Font.Size * 1.6f, FontStyle.Bold);
        _listStrip.FlatAppearance.BorderSize = 0;
        _listStrip.FlatAppearance.MouseOverBackColor = SystemColors.Highlight;
        _toolTip.SetToolTip(_listStrip, "Show the list (Ctrl+Alt+Home)");
        Controls.Add(_split);
        Controls.Add(_listStrip);

        ResumeLayout(false);

        _sessions.SessionChanged += (_, id) => OnSessionChanged(id);
        _list.StatusOf = StatusOf;
        _addButton.Click += (_, _) => AddConnection();
        _clock.Click += (_, _) => ChooseClockTimeZone();
        _fullScreenButton.Click += (_, _) => ToggleMainFullScreen();
        _moreButton.Click += (_, _) => ShowMoreMenu();
        _listStrip.Click += (_, _) => ToggleList();
        _list.ConnectionSelected += (_, e) => ShowConnection(e.Connection);
        _list.ConnectionActivated += (_, e) => OpenConnection(e.Connection);
        _list.MenuRequested += (_, e) => ShowConnectionMenu(e.Connection.Id, e.ScreenLocation);
        _list.DeleteRequested += (_, e) => {
            _menuConnectionId = e.Connection.Id;
            DeleteConnection();
        };
        _list.MoveRequested += (_, e) => Save(s => SettingsEditor.MoveConnection(s, e.ConnectionId, e.Drop));
        _list.GroupMoveRequested += (_, e) => Save(s => SettingsEditor.MoveGroup(s, e.Group, e.BeforeGroup));
        _list.GroupCollapseChanged += (_, e) => Save(s => SettingsEditor.SetGroupCollapsed(s, e.Group, e.Collapsed));
        _mainView.Overlay.PrimaryClicked += (_, _) => OnMainPrimary();
        _mainView.Overlay.SecondaryClicked += (_, _) => OnMainSecondary();
        _store.Changed += (_, _) => ShowSettings();

        ShowSettings();
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        var area = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width * 4 / 5), Math.Min(Height, area.Height * 4 / 5));
        CenterToScreen();
        _split.SplitterDistance = LogicalToDeviceUnits(300);
        ApplyListMinimum();
        _clock.MinimumWidthChanged += (_, _) => ApplyListMinimum();
        _split.SplitterMoved += (_, _) => SnapList();
        _list.CompactWidthChanged += (_, _) => ApplyListMinimum();
        _hotkeys = new HotkeyHook(OnHotkey);
        _list.Focus();
    }

    // Open sessions are closed without asking (decided on 2026-11-13).
    protected override void OnFormClosing(FormClosingEventArgs e) {
        base.OnFormClosing(e);
        if (e.Cancel) {
            return;
        }
        _closing = true;
        _hotkeys?.Dispose();
        _sessions.Dispose();
        foreach (var window in _fullScreens.Values.ToList()) {
            window.CloseFromCode();
        }
        foreach (var window in _windows.Values.ToList()) {
            window.CloseFromCode();
        }
        _fullScreens.Clear();
        _windows.Clear();
    }

    // The Palette colors are fixed values, so they are mixed again when Windows changes its colors.
    protected override void OnSystemColorsChanged(EventArgs e) {
        base.OnSystemColorsChanged(e);
        Palette.ResetAccent();
        ApplyPalette();
        _list.Invalidate();
    }

    // A new accent color in the Windows settings comes as a colorization change and as an
    // "ImmersiveColorSet" setting change.
    protected override void WndProc(ref Message m) {
        if (m.Msg == NativeMethods.WmDwmColorizationColorChanged
            || (m.Msg == NativeMethods.WmSettingChange && m.LParam != IntPtr.Zero
                && Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet")) {
            Palette.ResetAccent();
            _list.Invalidate();
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        _toolTip.Dispose();
        _stripFont.Dispose();
        _glyphFont.Dispose();
        base.OnFormClosed(e);
    }

    private IRdpClient CreateRdpClient() {
        var control = new RdpClientControl();
        _mainView.Attach(control);
        control.CreateControl();
        control.RequestGoFullScreen += (sender, _) => BeginInvoke(() => EnterFullScreen(IdOf(sender)));
        control.RequestLeaveFullScreen += (sender, _) => BeginInvoke(() => LeaveFullScreen(IdOf(sender)));
        return control;
    }

    private Guid? IdOf(object? client) => _sessions.Sessions.FirstOrDefault(s => s.Client == client)?.Connection.Id;

    private static RdpClientControl ControlOf(Session session) => (RdpClientControl)session.Client;

    private bool IsAway(Guid id) => _windows.ContainsKey(id) || _fullScreens.ContainsKey(id);

    private ConnectionStatus StatusOf(Guid connectionId) => _sessions.Find(connectionId)?.State switch {
        SessionState.Connected => ConnectionStatus.Connected,
        SessionState.Connecting or SessionState.Reconnecting => ConnectionStatus.Busy,
        SessionState.Failed => ConnectionStatus.Error,
        _ => ConnectionStatus.NotConnected,
    };

    private void ShowSettings() {
        var ids = _store.Current.Connections.Select(c => c.Id).ToHashSet();
        foreach (var id in _fullScreens.Keys.Where(id => !ids.Contains(id)).ToList()) {
            LeaveFullScreen(id);
        }
        foreach (var id in _windows.Keys.Where(id => !ids.Contains(id)).ToList()) {
            ReturnToMain(id, disconnect: false);
        }
        _sessions.CloseMissing(ids);
        _list.ShowSettings(_store.Current);
        ApplyBackground();
        _clock.ZoneId = _store.Current.ClockTimeZone;
        _toolTip.SetToolTip(_clock, $"{_clock.ZoneName}\nClick to change the time zone.");
        if (FindConnection(_list.ActiveConnectionId) is null) {
            _list.ActiveConnectionId = null;
        }
        RefreshViews();
    }

    private void OnSessionChanged(Guid connectionId) {
        if (_closing) {
            return;
        }
        _list.Invalidate();
        var session = _sessions.Find(connectionId);
        if (_fullScreens.ContainsKey(connectionId) && session?.State is not (SessionState.Connected or SessionState.Reconnecting)) {
            LeaveFullScreen(connectionId);
        }
        if (session?.State == SessionState.Connected) {
            _hotkeys?.Reinstall();
        }
        RefreshViews();
    }

    private void RefreshViews() {
        RenderMainView();
        foreach (var (id, window) in _windows) {
            RenderWindow(id, window);
        }
    }

    // Every session control in the main window stays visible and docked, the active one on top: a hidden
    // control is left out of layout, so it would keep its old size and its session the old resolution.
    private void RenderMainView() {
        var connection = FindConnection(_list.ActiveConnectionId);
        var active = connection is null ? null : _sessions.Find(connection.Id);
        foreach (var session in _sessions.Sessions) {
            var control = ControlOf(session);
            if (control.Parent == _mainView) {
                control.Visible = true;
                control.TabStop = session == active;
                if (session == active) {
                    control.BringToFront();
                }
            }
        }
        if (connection is null) {
            _mainView.Overlay.ShowState("", _store.Current.Connections.Count == 0
                ? "Add a connection with the button at the bottom left."
                : "Select a connection.");
            return;
        }
        if (active is not null && IsAway(connection.Id)) {
            var inSeparateWindow = _windows.ContainsKey(connection.Id);
            _mainView.Overlay.ShowState(
                connection.Name,
                _fullScreens.ContainsKey(connection.Id) ? "This session is in full screen." : "This session is open in a separate window.",
                "Show it",
                inSeparateWindow ? ReturnText : "");
            return;
        }
        RenderState(_mainView, connection, active, failedSecondary: "Edit connection...", secondary: "");
    }

    private void RenderWindow(Guid id, SessionWindow window) {
        if (FindConnection(id) is not { } connection) {
            return;
        }
        window.Text = $"{connection.Name} - {Messages.Title}";
        if (_fullScreens.ContainsKey(id)) {
            window.View.Overlay.ShowState(connection.Name, "This session is in full screen.", "Show it");
            return;
        }
        RenderState(window.View, connection, _sessions.Find(id), failedSecondary: ReturnText, secondary: ReturnText);
    }

    private static void RenderState(SessionView view, Connection connection, Session? session, string failedSecondary, string secondary) {
        switch (session?.State) {
            case null:
                view.Overlay.ShowState(connection.Name, "Not connected.", "Connect", secondary);
                break;
            case SessionState.Connecting:
                view.Overlay.ShowState(connection.Name, session.Message, "Cancel", secondary);
                break;
            case SessionState.Connected or SessionState.Reconnecting:
                view.Overlay.Visible = false;
                break;
            case SessionState.Failed:
                view.Overlay.ShowState(connection.Name, session.Message, "Reconnect", failedSecondary, isError: true);
                break;
            default:
                view.Overlay.ShowState(connection.Name, session.Message, "Connect", secondary);
                break;
        }
    }

    // A click on a row shows its session, and a session in another window is brought forward. A connection
    // without a running session only shows the session area with its "Connect" button; a double click
    // connects it (decided on 2026-11-18).
    private void ShowConnection(Connection connection) {
        _list.ActiveConnectionId = connection.Id;
        RefreshViews();
        if (IsAway(connection.Id)) {
            ShowAway(connection.Id);
        }
    }

    private void OpenConnection(Connection connection) {
        ShowConnection(connection);
        if (!IsAway(connection.Id) && _sessions.Find(connection.Id)?.IsActive != true) {
            Connect(connection, this);
        }
    }

    private void OnMainPrimary() {
        if (FindConnection(_list.ActiveConnectionId) is not { } connection) {
            return;
        }
        if (IsAway(connection.Id)) {
            ShowAway(connection.Id);
        } else {
            ConnectOrCancel(connection, this);
        }
    }

    private void OnMainSecondary() {
        if (_list.ActiveConnectionId is not { } id) {
            return;
        }
        if (_windows.ContainsKey(id)) {
            ReturnToMain(id, disconnect: false);
        } else {
            EditConnection(id);
        }
    }

    private void ConnectOrCancel(Connection connection, IWin32Window owner) {
        var session = _sessions.Find(connection.Id);
        if (session?.State == SessionState.Connecting) {
            session.Disconnect();
        } else {
            Connect(connection, owner);
        }
    }

    private void Connect(Connection connection, IWin32Window owner) {
        if (connection.Kind == ConnectionKind.Network && connection.Password is not { IsEmpty: false }) {
            using var form = new CredentialsForm(connection);
            if (form.ShowDialog(owner) != DialogResult.OK) {
                return;
            }
            connection = connection with { UserName = form.UserName, Password = form.Password };
            if (form.SavePassword) {
                var saved = connection;
                Save(s => SettingsEditor.UpdateConnection(s, saved));
            }
        }
        var session = _sessions.GetOrCreate(connection);
        RefreshViews();
        session.Connect(connection);
    }

    private void ShowAway(Guid id) {
        Form form = _fullScreens.TryGetValue(id, out var fullScreen) ? fullScreen : _windows[id];
        if (form.WindowState == FormWindowState.Minimized) {
            form.WindowState = FormWindowState.Normal;
        }
        form.Activate();
    }

    private void OpenInSeparateWindow(Guid? id) {
        if (id is not { } connectionId || IsAway(connectionId)
            || _sessions.Find(connectionId) is not { } session || FindConnection(connectionId) is not { } connection) {
            return;
        }
        var offset = LogicalToDeviceUnits(40);
        var window = new SessionWindow(connectionId, connection.Name, new Rectangle(Left + offset, Top + offset, Width, Height));
        window.View.Overlay.PrimaryClicked += (_, _) => {
            if (_fullScreens.ContainsKey(connectionId)) {
                ShowAway(connectionId);
            } else if (FindConnection(connectionId) is { } current) {
                ConnectOrCancel(current, window);
            }
        };
        window.View.Overlay.SecondaryClicked += (_, _) => ReturnToMain(connectionId, disconnect: false);
        // Closing the window disconnects the session (decided on 2026-11-13).
        window.ClosedByUser += (_, _) => ReturnToMain(connectionId, disconnect: true, closeWindow: false);
        _windows[connectionId] = window;
        window.Show();
        var control = ControlOf(session);
        window.View.Attach(control);
        control.Visible = true;
        RefreshViews();
        window.Activate();
        _log.Information(connection.Name, "Moved to a separate window.");
    }

    private void ReturnToMain(Guid? id, bool disconnect, bool closeWindow = true) {
        if (id is not { } connectionId) {
            return;
        }
        if (_fullScreens.ContainsKey(connectionId)) {
            LeaveFullScreen(connectionId);
        }
        if (!_windows.Remove(connectionId, out var window)) {
            return;
        }
        if (_sessions.Find(connectionId) is { } session) {
            _mainView.Attach(ControlOf(session));
            _log.Information(session.Connection.Name, disconnect ? "The separate window was closed." : "Returned to the main window.");
            if (disconnect) {
                session.Disconnect();
            }
        }
        if (closeWindow) {
            window.CloseFromCode();
        }
        if (_closing) {
            return;
        }
        _list.ActiveConnectionId = connectionId;
        RefreshViews();
        Activate();
    }

    private void ToggleFullScreen(Guid? id) {
        if (id is not { } connectionId
            || _sessions.Find(connectionId) is not { State: SessionState.Connected or SessionState.Reconnecting } session) {
            return;
        }
        var entering = !_fullScreens.ContainsKey(connectionId);
        // The component answers with RequestGoFullScreen or RequestLeaveFullScreen; without it, do it here.
        if (!ControlOf(session).TrySetFullScreen(entering)) {
            if (entering) {
                EnterFullScreen(connectionId);
            } else {
                LeaveFullScreen(connectionId);
            }
        }
    }

    private void EnterFullScreen(Guid? id) {
        if (_closing || id is not { } connectionId || _fullScreens.ContainsKey(connectionId)
            || _sessions.Find(connectionId) is not { } session || FindConnection(connectionId) is not { } connection) {
            return;
        }
        var control = ControlOf(session);
        var window = new FullScreenWindow(connectionId, connection.Name, Screen.FromControl(control).Bounds);
        window.ClosedByUser += (_, _) => LeaveFullScreen(connectionId, closeWindow: false);
        _fullScreens[connectionId] = window;
        window.Show();
        window.View.Attach(control);
        window.View.Overlay.Visible = false;
        control.Visible = true;
        window.Activate();
        control.Focus();
        _hotkeys?.Reinstall();
        _log.Information(connection.Name, "Full screen.");
        RefreshViews();
    }

    private void LeaveFullScreen(Guid? id, bool closeWindow = true) {
        if (id is not { } connectionId || !_fullScreens.Remove(connectionId, out var window)) {
            return;
        }
        if (_sessions.Find(connectionId) is { } session) {
            var control = ControlOf(session);
            var host = _windows.TryGetValue(connectionId, out var separate) ? separate.View : _mainView;
            host.Attach(control);
            control.TrySetFullScreen(false);
            if (host == _mainView) {
                _list.ActiveConnectionId = connectionId;
            }
            _log.Information(session.Connection.Name, "Left full screen.");
        }
        if (closeWindow) {
            window.CloseFromCode();
        }
        if (_closing) {
            return;
        }
        RefreshViews();
        (_windows.GetValueOrDefault(connectionId) as Form ?? this).Activate();
    }

    // The doodle background of the left panel, chosen in the "..." menu; the session area stays plain
    // gray (both decided on 2026-11-19).
    private void ApplyBackground() {
        var theme = Wallpaper.Theme(ListBackground.Of(_store.Current));
        if (theme != _leftPanel.Theme) {
            _leftPanel.Theme = theme;
        }
    }

    private void ToggleList() {
        _split.Panel1Collapsed = !_split.Panel1Collapsed;
        _listStrip.Visible = _split.Panel1Collapsed;
    }

    // The full list gets as narrow as the clock lets it, so the city and its UTC offset stay whole; below
    // that only the compact strip of circles is left (both decided on 2026-11-19).
    private void ApplyListMinimum() {
        _split.Panel1MinSize = _list.CompactWidth;
        SnapList();
    }

    // A list dragged narrower than the clock goes to the compact strip or back to the clock's width,
    // whichever is nearer.
    private void SnapList() {
        var full = _clock.MinimumWidth;
        var compact = _list.CompactWidth;
        var width = _split.SplitterDistance;
        if (width < full) {
            var snapped = width - compact < full - width ? compact : full;
            if (snapped != width) {
                _split.SplitterDistance = snapped;
            }
        }
        SetCompactList(_split.SplitterDistance < full);
    }

    // The compact strip has room only for the time of the clock, and its buttons stand one under
    // another, as wide as the strip.
    private void SetCompactList(bool compact) {
        if (_list.Compact == compact) {
            return;
        }
        _list.Compact = compact;
        _clock.Compact = compact;
        _bottomBar.SuspendLayout();
        if (compact) {
            _bottomBar.SetCellPosition(_addButton, new TableLayoutPanelCellPosition(0, 0));
            _bottomBar.SetCellPosition(_fullScreenButton, new TableLayoutPanelCellPosition(0, 1));
            _bottomBar.SetCellPosition(_moreButton, new TableLayoutPanelCellPosition(0, 2));
            _bottomBar.ColumnCount = 1;
            _bottomBar.RowCount = 3;
            _bottomBar.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
        } else {
            _bottomBar.ColumnCount = 4;
            _bottomBar.RowCount = 1;
            _bottomBar.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
            _bottomBar.SetCellPosition(_addButton, new TableLayoutPanelCellPosition(0, 0));
            _bottomBar.SetCellPosition(_fullScreenButton, new TableLayoutPanelCellPosition(2, 0));
            _bottomBar.SetCellPosition(_moreButton, new TableLayoutPanelCellPosition(3, 0));
        }
        foreach (var button in new[] { _addButton, _fullScreenButton, _moreButton }) {
            button.Anchor = compact ? AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.Top | AnchorStyles.Left;
        }
        _bottomBar.ResumeLayout(true);
    }

    // The main window covers its monitor, taskbar included, with no title bar; the list stays. Only the
    // border style changes, so the window and its session controls are kept.
    private void ToggleMainFullScreen() {
        if (_mainFullScreen) {
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _windowedBounds;
            if (_windowedMaximized) {
                WindowState = FormWindowState.Maximized;
            }
        } else {
            _windowedMaximized = WindowState == FormWindowState.Maximized;
            _windowedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var monitor = Screen.FromControl(this).Bounds;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = monitor;
        }
        _mainFullScreen = !_mainFullScreen;
        ShowMainFullScreenState();
    }

    // The buttons have no margins and the bar no padding, so a hovered button is filled up to the edges
    // like the clock and the rows of the list (decided on 2026-11-19).
    private GlyphButton CreateIconButton(string glyph, string name) {
        var button = new GlyphButton {
            Glyph = glyph,
            Font = _glyphFont,
            Size = new Size(40, 40),
            Margin = Padding.Empty,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = SystemColors.ControlText,
            AccessibleName = name,
        };
        button.FlatAppearance.BorderSize = 0;
        _toolTip.SetToolTip(button, name);
        _iconButtons.Add(button);
        return button;
    }

    private void ApplyPalette() {
        foreach (var button in _iconButtons) {
            button.FlatAppearance.MouseOverBackColor = Palette.Hover;
            button.FlatAppearance.MouseDownBackColor = Palette.Pressed;
        }
        _bottomLine.BackColor = Palette.Separator;
    }

    private void ShowMainFullScreenState() {
        var name = _mainFullScreen ? "Back to a window" : "Full screen with the list";
        _fullScreenButton.Glyph = _mainFullScreen ? WindowGlyph : FullScreenGlyph;
        _fullScreenButton.AccessibleName = name;
        _toolTip.SetToolTip(_fullScreenButton, name);
    }

    private void OnHotkey(Hotkey hotkey) {
        if (_closing) {
            return;
        }
        var form = ActiveForm;
        switch (hotkey) {
            case Hotkey.FullScreen:
                ToggleFullScreen(CurrentSessionId(form));
                break;
            case Hotkey.ToggleList when form == this:
                ToggleList();
                break;
            case Hotkey.NextSession:
                CycleSession(form, forward: true);
                break;
            case Hotkey.PreviousSession:
                CycleSession(form, forward: false);
                break;
        }
    }

    private Guid? CurrentSessionId(Form? form) => form switch {
        SessionWindow window => window.ConnectionId,
        FullScreenWindow fullScreen => fullScreen.ConnectionId,
        _ => _list.ActiveConnectionId,
    };

    private void CycleSession(Form? form, bool forward) {
        var order = ConnectionListLayout.ConnectionOrder(_store.Current);
        var next = SessionCycle.Next(order, CurrentSessionId(form), id => _sessions.Find(id)?.IsActive == true, forward);
        if (next is not { } id) {
            return;
        }
        _list.ActiveConnectionId = id;
        RefreshViews();
        if (IsAway(id)) {
            ShowAway(id);
            return;
        }
        if (WindowState == FormWindowState.Minimized) {
            WindowState = FormWindowState.Normal;
        }
        Activate();
        if (_sessions.Find(id) is { } session) {
            ControlOf(session).Focus();
        }
    }

    private void ShowConnectionMenu(Guid id, Point screenLocation) {
        _menuConnectionId = id;
        var session = _sessions.Find(id);
        var menu = new NativeMenu()
            .Add("Edit...", () => EditConnection(id))
            .Add("Disconnect", () => _sessions.Find(id)?.Disconnect(), enabled: session?.IsActive == true)
            .Add("Open in separate window", () => OpenInSeparateWindow(id), enabled: session is not null && !IsAway(id))
            .Add(ReturnText, () => ReturnToMain(id, disconnect: false), enabled: _windows.ContainsKey(id))
            .Add("Full screen", () => ToggleFullScreen(id), enabled: session?.State is SessionState.Connected or SessionState.Reconnecting,
                shortcut: "Ctrl+Alt+Break");
        // Only a VM's console needs it: the guest's sign-in screen waits for Ctrl+Alt+Del.
        if (FindConnection(id) is { Kind: ConnectionKind.HyperV, BasicSession: true }) {
            menu.Add("Send Ctrl+Alt+Del", () => SendCtrlAltDel(id), enabled: session?.State == SessionState.Connected);
        }
        menu.AddSeparator()
            .Add("Delete", () => {
                _menuConnectionId = id;
                DeleteConnection();
            });
        menu.Show(this, screenLocation);
    }

    // Above the button: to its left in the full list, to its right in the compact strip.
    private void ShowMoreMenu() {
        var current = ListBackground.Of(_store.Current);
        var background = new NativeMenu();
        foreach (var name in ListBackground.Names) {
            background.Add(name, () => Save(s => s with { Background = name }), isChecked: name == current);
        }
        var menu = new NativeMenu()
            .Add("Hide list", ToggleList, shortcut: "Ctrl+Alt+Home")
            .Add("Clock time zone...", ChooseClockTimeZone)
            .AddSubmenu("Background", background)
            .Add("Change master password...", ChangeMasterPassword)
            .Add("Log", ShowLog)
            .Add("About RemoteMe", () => {
                using var about = new AboutForm(_store.Path, _logPath);
                about.ShowDialog(this);
            });
        var button = _moreButton.RectangleToScreen(_moreButton.ClientRectangle);
        if (_list.Compact) {
            menu.Show(this, new Point(button.Right, button.Bottom), NativeMethods.TpmLeftAlign | NativeMethods.TpmBottomAlign, button);
        } else {
            menu.Show(this, new Point(button.Right, button.Top), NativeMethods.TpmRightAlign | NativeMethods.TpmBottomAlign, button);
        }
    }

    private void SendCtrlAltDel(Guid? connectionId) {
        if (_sessions.Find(connectionId ?? Guid.Empty) is not { State: SessionState.Connected } session) {
            return;
        }
        var id = session.Connection.Id;
        if (IsAway(id)) {
            ShowAway(id);
        } else {
            _list.ActiveConnectionId = id;
            RefreshViews();
        }
        ControlOf(session).SendCtrlAltDel();
    }

    private void AddConnection() {
        using var form = new ConnectionForm(null, _store.Current.Groups);
        if (form.ShowDialog(this) == DialogResult.OK && form.Result is { } connection) {
            if (Save(s => SettingsEditor.AddConnection(s, connection))) {
                _list.ActiveConnectionId = connection.Id;
                RefreshViews();
            }
        }
    }

    private void EditConnection(Guid? connectionId) {
        if (FindConnection(connectionId) is not { } original) {
            return;
        }
        using var form = new ConnectionForm(original, _store.Current.Groups);
        if (form.ShowDialog(this) == DialogResult.OK && form.Result is { } connection) {
            Save(s => SettingsEditor.UpdateConnection(s, connection));
        }
    }

    private void DeleteConnection() {
        if (FindConnection(_menuConnectionId) is not { } connection) {
            return;
        }
        var question = _sessions.Find(connection.Id)?.IsActive == true
            ? $"Delete the connection \"{connection.Name}\"? Its session will be disconnected."
            : $"Delete the connection \"{connection.Name}\"?";
        if (Messages.Confirm(this, question)) {
            Save(s => SettingsEditor.RemoveConnection(s, connection.Id));
        }
    }

    private void ChooseClockTimeZone() {
        using var form = new TimeZoneForm(_store.Current.ClockTimeZone);
        if (form.ShowDialog(this) == DialogResult.OK && form.ZoneId != _store.Current.ClockTimeZone) {
            var zoneId = form.ZoneId;
            Save(s => s with { ClockTimeZone = zoneId });
        }
    }

    private void ChangeMasterPassword() {
        using var form = new ChangePasswordForm(_store);
        if (form.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        var newPassword = form.NewPassword;
        Cursor.Current = Cursors.WaitCursor;
        if (Save(overwrite => _store.ChangeMasterPassword(newPassword, overwrite))) {
            _log.Information(LogSource, "The master password was changed.");
            Messages.Information(this, "The master password was changed.");
        }
    }

    private void ShowLog() {
        if (_logForm is null || _logForm.IsDisposed) {
            _logForm = new LogForm(_log, _logPath);
            _logForm.Show(this);
        } else {
            _logForm.Activate();
        }
    }

    private bool Save(Func<Settings, Settings> change) => Save(overwrite => _store.Apply(change, overwrite));

    // Saves and, when the file was changed outside, asks whether to read it again or overwrite it.
    private bool Save(Action<bool> save) {
        try {
            save(false);
            return true;
        } catch (VaultException ex) when (ex.Error == VaultError.Conflict) {
            _log.Warning(LogSource, "The settings file was changed outside RemoteMe.");
            switch (Messages.AskConflict(this)) {
                case ConflictChoice.Overwrite:
                    try {
                        save(true);
                        _log.Information(LogSource, "The settings file was overwritten.");
                        return true;
                    } catch (Exception inner) when (IsFileError(inner)) {
                        ReportSaveError(inner);
                        return false;
                    }
                case ConflictChoice.Reload:
                    Reload();
                    return false;
                default:
                    return false;
            }
        } catch (Exception ex) when (IsFileError(ex)) {
            ReportSaveError(ex);
            return false;
        }
    }

    private void Reload() {
        try {
            _store.Reload();
            _log.Information(LogSource, "The settings file was read again.");
        } catch (VaultException ex) when (ex.Error == VaultError.WrongPassword) {
            using var form = UnlockForm.ForReload(_store);
            form.ShowDialog(this);
        } catch (Exception ex) when (IsFileError(ex)) {
            _log.Error(LogSource, $"The settings file could not be read: {ex.Message}");
            Messages.Error(this, $"The settings file could not be read.\n\n{ex.Message}");
        }
    }

    private void ReportSaveError(Exception exception) {
        _log.Error(LogSource, $"The settings could not be saved: {exception.Message}");
        Messages.Error(this, $"The settings could not be saved.\n\n{exception.Message}");
    }

    private Connection? FindConnection(Guid? id) =>
        id is null ? null : _store.Current.Connections.FirstOrDefault(c => c.Id == id);

    private static bool IsFileError(Exception exception) =>
        exception is VaultException or IOException or UnauthorizedAccessException;
}
