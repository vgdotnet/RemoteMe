using System.ComponentModel;
using System.Runtime.InteropServices;
using RdpProbe.Interop;
using static RdpProbe.NativeMethods;

namespace RdpProbe;

[DesignerCategory("Code")]
internal sealed class ProbeForm : Form {
    private const int HyperVPort = 2179;
    private const int DefaultRdpPort = 3389;
    private const string HyperVServiceClass = "Microsoft Virtual Console Service";
    private const int FieldWidth = 290;

    private readonly TextBox _log;
    private readonly Panel _sessionArea;
    private readonly RdpEventSink _sink;
    private readonly RdpClientHost _host;
    private readonly KeyboardProbe _keyboard;
    private readonly System.Windows.Forms.Timer _displayTimer;

    private readonly RadioButton _modeHyperV;
    private readonly TextBox _server;
    private readonly TextBox _vmId;
    private readonly CheckBox _enhancedMode;
    private readonly TextBox _serviceClass;
    private readonly CheckBox _disableDelegation;
    private readonly TextBox _user;
    private readonly TextBox _domain;
    private readonly TextBox _password;
    private readonly CheckBox _credSsp;
    private readonly CheckBox _negotiate;
    private readonly ComboBox _authLevel;
    private readonly CheckBox _notifyPublicKey;
    private readonly CheckBox _rejectPublicKey;
    private readonly CheckBox _allowPrompts;
    private readonly CheckBox _allowSaving;
    private readonly CheckBox _autoReconnect;
    private readonly NumericUpDown _maxReconnect;
    private readonly ComboBox _keyboardHookMode;
    private readonly CheckBox _passScale;
    private readonly CheckBox _autoApplyDisplay;

    private SessionWindow? _separateWindow;
    private bool _movedWithSetParent;
    private bool _loggedIn;
    private string _defaultServiceClass = "";

    public ProbeForm() {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RdpProbe";

        _log = new TextBox {
            Name = "Log",
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F),
        };
        _sessionArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 32) };
        _sink = new RdpEventSink(Log);
        _host = new RdpClientHost(_sink) { Dock = DockStyle.Fill };
        _keyboard = new KeyboardProbe(Log, IsInsideSession);
        _displayTimer = new System.Windows.Forms.Timer { Interval = 300 };

        var settings = new FlowLayoutPanel {
            Dock = DockStyle.Left,
            Width = 330,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(6),
        };

        AddSection(settings, "Target");
        var modeNetwork = AddRadio(settings, "Network address", true);
        _modeHyperV = AddRadio(settings, "VM on a Hyper-V host", false);
        _server = AddText(settings, "Server (computer or Hyper-V host)");
        _server.Name = "Server";
        _vmId = AddText(settings, "VM ID (Hyper-V only)");
        _enhancedMode = AddCheck(settings, "Enhanced session (PCB ;EnhancedMode=1)", false);
        _serviceClass = AddText(settings, "Authentication service class (Hyper-V)");
        _disableDelegation = AddCheck(settings, "Do not delegate credentials (DisableCredentialsDelegation)", false);

        AddSection(settings, "Credentials");
        _user = AddText(settings, "User name");
        _user.Name = "User";
        _domain = AddText(settings, "Domain (optional)");
        _password = AddText(settings, "Password (kept in memory only)");
        _password.Name = "Password";
        _password.UseSystemPasswordChar = true;

        AddSection(settings, "Security");
        _credSsp = AddCheck(settings, "CredSSP (NLA)", true);
        _negotiate = AddCheck(settings, "Negotiate security layer", true);
        _authLevel = AddCombo(settings, "Server authentication level", 2,
            "0 - connect without warning",
            "1 - do not connect if authentication fails",
            "2 - warn if authentication fails");
        _notifyPublicKey = AddCheck(settings, "Report the server public key (NotifyTSPublicKey)", true);
        _rejectPublicKey = AddCheck(settings, "Reject the public key (stop logon)", false);
        _allowPrompts = AddCheck(settings, "Allow credential prompts", false);
        _allowSaving = AddCheck(settings, "Allow \"Remember me\" in prompts", false);

        AddSection(settings, "Reconnect");
        _autoReconnect = AddCheck(settings, "Automatic reconnect by the component", true);
        AddLabel(settings, "Max reconnect attempts");
        _maxReconnect = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 20, Width = 80 };
        settings.Controls.Add(_maxReconnect);

        AddSection(settings, "Keyboard");
        _keyboardHookMode = AddCombo(settings, "Keyboard hook mode (KeyboardHookMode)", 2,
            "0 - combinations stay on this computer",
            "1 - combinations go to the session",
            "2 - to the session in full screen only");
        var swallowMessageLoop = AddCheck(settings, "Swallow combinations in the message loop", false);
        var swallowHook = AddCheck(settings, "Swallow combinations in the low-level hook", false);
        AddButton(settings, "Install or reinstall the low-level hook", () => _keyboard.InstallHook());
        AddButton(settings, "Remove the low-level hook", () => _keyboard.RemoveHook());

        AddSection(settings, "Display");
        _passScale = AddCheck(settings, "Pass the monitor scale to the session", true);
        _autoApplyDisplay = AddCheck(settings, "Apply after resize and DPI change", true);
        AddButton(settings, "Apply size and scale now", () => ApplyDisplay("button"));

        AddSection(settings, "Session");
        AddButton(settings, "Connect", Connect);
        AddButton(settings, "Disconnect", () => _host.Client.Disconnect());
        AddButton(settings, "Reconnect (IMsRdpClient8.Reconnect)", Reconnect);
        AddButton(settings, "Send Ctrl+Alt+Del (SendKeys)", SendCtrlAltDel);
        AddButton(settings, "Log state", LogState);

        AddSection(settings, "Separate window");
        AddButton(settings, "Move: WinForms Parent", () => MoveToSeparateWindow(useSetParent: false));
        AddButton(settings, "Move: Win32 SetParent", () => MoveToSeparateWindow(useSetParent: true));
        AddButton(settings, "Return to the main window", () => ReturnToMainWindow(closeWindow: true));

        var logButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 };
        logButtons.Controls.Add(MakeButton("Copy log", () => {
            if (_log.TextLength > 0) {
                Clipboard.SetText(_log.Text);
            }
        }, 120));
        logButtons.Controls.Add(MakeButton("Clear log", () => _log.Clear(), 120));
        var logPanel = new Panel { Dock = DockStyle.Bottom, Height = 240 };
        logPanel.Controls.Add(_log);
        logPanel.Controls.Add(logButtons);

        _sink.AcceptPublicKey = _ => !_rejectPublicKey.Checked;
        _sink.LoginComplete += () => _loggedIn = true;
        _sink.Disconnected += OnDisconnected;
        _displayTimer.Tick += (_, _) => {
            _displayTimer.Stop();
            ApplyDisplay("auto");
        };

        modeNetwork.CheckedChanged += (_, _) => UpdateModeFields();
        swallowMessageLoop.CheckedChanged += (_, _) => _keyboard.SwallowInMessageLoop = swallowMessageLoop.Checked;
        swallowHook.CheckedChanged += (_, _) => _keyboard.SwallowInHook = swallowHook.Checked;
        _host.Resize += ScheduleDisplayUpdate;
        _host.DpiChangedAfterParent += ScheduleDisplayUpdate;
        DpiChanged += (_, e) => {
            Log($"main window DPI {e.DeviceDpiOld} -> {e.DeviceDpiNew}");
            ScheduleDisplayUpdate(this, EventArgs.Empty);
        };

        ((ISupportInitialize)_host).BeginInit();
        _sessionArea.Controls.Add(_host);
        Controls.Add(_sessionArea);
        Controls.Add(settings);
        Controls.Add(logPanel);
        ((ISupportInitialize)_host).EndInit();
        UpdateModeFields();
        ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        Application.AddMessageFilter(_keyboard);
        _keyboard.InstallHook();
        Run("startup", () => {
            Log($"control: MsRdpClient9NotSafeForScripting, version {_host.Client.Version}");
            _defaultServiceClass = _host.Client.AdvancedSettings9.AuthenticationServiceClass ?? "";
            Log($"control: default AuthenticationServiceClass \"{_defaultServiceClass}\"");
            Log($"main window DPI {GetDpiForWindow(Handle)}, session area {CurrentDisplay()}");
        });
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        Application.RemoveMessageFilter(_keyboard);
        _keyboard.Dispose();
        _displayTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void UpdateModeFields() {
        var hyperV = _modeHyperV.Checked;
        _vmId.Enabled = hyperV;
        _enhancedMode.Enabled = hyperV;
        _serviceClass.Enabled = hyperV;
        _negotiate.Checked = !hyperV;
        _disableDelegation.Checked = hyperV;
        if (hyperV && _serviceClass.TextLength == 0) {
            _serviceClass.Text = HyperVServiceClass;
        }
    }

    private void Connect() {
        if (string.IsNullOrWhiteSpace(_server.Text)) {
            Log("connect: the server is empty");
            return;
        }
        var client = _host.Client;
        if (client.Connected != 0) {
            Log("connect: the control is connected or connecting, press Disconnect first");
            return;
        }
        var advanced = client.AdvancedSettings9;
        var display = CurrentDisplay();
        var hyperV = _modeHyperV.Checked;

        Set("Server", () => client.Server = _server.Text.Trim());
        Set("UserName", () => client.UserName = _user.Text.Trim());
        Set("Domain", () => client.Domain = _domain.Text.Trim());
        Set("DesktopWidth", () => client.DesktopWidth = (int)display.Width);
        Set("DesktopHeight", () => client.DesktopHeight = (int)display.Height);
        Set("ColorDepth", () => client.ColorDepth = 32);
        if (_password.TextLength > 0) {
            Set("ClearTextPassword", () => advanced.ClearTextPassword = _password.Text);
        }
        Set("EnableCredSspSupport", () => advanced.EnableCredSspSupport = _credSsp.Checked);
        Set("NegotiateSecurityLayer", () => advanced.NegotiateSecurityLayer = _negotiate.Checked);
        Set("AuthenticationLevel", () => advanced.AuthenticationLevel = (uint)_authLevel.SelectedIndex);
        Set("NotifyTSPublicKey", () => advanced.NotifyTSPublicKey = _notifyPublicKey.Checked);
        Set("EnableAutoReconnect", () => advanced.EnableAutoReconnect = _autoReconnect.Checked);
        Set("MaxReconnectAttempts", () => advanced.MaxReconnectAttempts = (int)_maxReconnect.Value);
        Set("BitmapPersistence", () => advanced.BitmapPersistence = 0);
        Set("SmartSizing", () => advanced.SmartSizing = false);
        Set("RedirectClipboard", () => advanced.RedirectClipboard = true);
        Set("RedirectDrives", () => advanced.RedirectDrives = false);
        Set("RedirectPrinters", () => advanced.RedirectPrinters = false);
        Set("AudioRedirectionMode", () => advanced.AudioRedirectionMode = 0);
        Set("AudioCaptureRedirectionMode", () => advanced.AudioCaptureRedirectionMode = false);
        Set("GrabFocusOnConnect", () => advanced.GrabFocusOnConnect = true);
        Set("KeyboardHookMode", () => client.SecuredSettings3.KeyboardHookMode = _keyboardHookMode.SelectedIndex);
        Set("AllowPromptingForCredentials", () => _host.NonScriptable.SetAllowPromptingForCredentials(_allowPrompts.Checked));
        Set("AllowCredentialSaving", () => _host.NonScriptable.SetAllowCredentialSaving(_allowSaving.Checked));
        Set("RDPPort", () => advanced.RDPPort = hyperV ? HyperVPort : DefaultRdpPort);
        Set("PCB", () => advanced.PCB = hyperV ? _vmId.Text.Trim() + (_enhancedMode.Checked ? ";EnhancedMode=1" : "") : "");
        Set("AuthenticationServiceClass", () => advanced.AuthenticationServiceClass = hyperV ? _serviceClass.Text.Trim() : _defaultServiceClass);
        SetExtended("DisableCredentialsDelegation", _disableDelegation.Checked);
        SetScaleProperties(display);

        Log($"connect: {(hyperV ? "Hyper-V" : "network")} mode, {display}");
        Log($"connect: server {DescribeServer(_server.Text.Trim())}, user name {DescribeUserName(_user.Text.Trim())}, "
            + $"domain {(_domain.TextLength > 0 ? "set" : "empty")}, password {(_password.TextLength > 0 ? "set" : "empty")}");
        Log($"connect: CredSSP {_credSsp.Checked}, negotiate {_negotiate.Checked}, authentication level {_authLevel.SelectedIndex}, "
            + $"delegation {(_disableDelegation.Checked ? "disabled" : "enabled")}, prompts {_allowPrompts.Checked}, "
            + $"remember me {_allowSaving.Checked}");
        if (hyperV) {
            Log($"connect: VM ID {(Guid.TryParse(_vmId.Text.Trim(), out _) ? "is a GUID" : "is NOT a GUID")}, "
                + $"enhanced {_enhancedMode.Checked}, service class \"{_serviceClass.Text.Trim()}\"");
        }
        Run("Connect", client.Connect);
    }

    private void SetScaleProperties(SessionDisplay display) {
        SetExtended("DesktopScaleFactor", display.DesktopScaleFactor);
        SetExtended("DeviceScaleFactor", display.DeviceScaleFactor);
    }

    private void SetExtended(string name, object value) {
        Set(name, () => _host.ExtendedSettings.SetProperty(name, ref value));
    }

    private void ApplyDisplay(string reason) {
        if (!_loggedIn) {
            Log($"display ({reason}): skipped, not logged in");
            return;
        }
        var display = CurrentDisplay();
        Run($"display ({reason}): UpdateSessionDisplaySettings {display}", () => {
            _host.Client.UpdateSessionDisplaySettings(
                display.Width,
                display.Height,
                display.PhysicalWidthMm,
                display.PhysicalHeightMm,
                0,
                display.DesktopScaleFactor,
                display.DeviceScaleFactor);
            Log($"display ({reason}): UpdateSessionDisplaySettings {display}");
        });
    }

    private void Reconnect() {
        var display = CurrentDisplay();
        var status = _host.Client.Reconnect(display.Width, display.Height);
        Log($"Reconnect({display.Width}x{display.Height}) -> {status}");
    }

    private void SendCtrlAltDel() {
        // Scan codes Ctrl 0x1D, Alt 0x38, Del 0x53: three presses, then three releases.
        int[] keyData = [0x1D, 0x38, 0x53, 0x53, 0x38, 0x1D];
        short[] keyUp = [0, 0, 0, -1, -1, -1];
        var keyUpHandle = GCHandle.Alloc(keyUp, GCHandleType.Pinned);
        var keyDataHandle = GCHandle.Alloc(keyData, GCHandleType.Pinned);
        try {
            _host.Focus();
            _host.NonScriptable.SendKeys(keyData.Length, keyUpHandle.AddrOfPinnedObject(), keyDataHandle.AddrOfPinnedObject());
            Log("keys: Ctrl+Alt+Del sent with SendKeys");
        } finally {
            keyUpHandle.Free();
            keyDataHandle.Free();
        }
    }

    private void LogState() {
        var client = _host.Client;
        Log($"state: Connected={client.Connected}, desktop {client.DesktopWidth}x{client.DesktopHeight}, logged in={_loggedIn}");
        Run("state: CanAutoReconnect", () => Log($"state: CanAutoReconnect={client.AdvancedSettings9.CanAutoReconnect}"));
        Run("state: DesktopScaleFactor", () => Log($"state: DesktopScaleFactor={_host.ExtendedSettings.GetProperty("DesktopScaleFactor")}"));
        Run("state: DeviceScaleFactor", () => Log($"state: DeviceScaleFactor={_host.ExtendedSettings.GetProperty("DeviceScaleFactor")}"));
        Log($"state: session area {CurrentDisplay()}, host window 0x{_host.Handle:X}, in separate window={_separateWindow is not null}");
        LogChildWindows();
    }

    private void LogChildWindows() {
        var uiThread = GetCurrentThreadId();
        var windows = new List<(string ClassName, bool OnUiThread)>();
        var buffer = new char[256];
        EnumChildWindows(_host.Handle, (hwnd, lParam) => {
            var length = GetClassName(hwnd, buffer, buffer.Length);
            var threadId = GetWindowThreadProcessId(hwnd, out var processId);
            windows.Add((new string(buffer, 0, length), threadId == uiThread));
            return true;
        }, IntPtr.Zero);
        Log($"state: {windows.Count} child windows of the control");
        foreach (var group in windows.GroupBy(w => w)) {
            Log($"state:   {group.Key.ClassName} x{group.Count()} on {(group.Key.OnUiThread ? "the UI thread" : "another thread")}");
        }
    }

    private void OnDisconnected(int reason) {
        _loggedIn = false;
        Run("disconnect details", () => {
            var client = _host.Client;
            var extended = client.ExtendedDisconnectReason;
            var description = client.GetErrorDescription((uint)reason, (uint)extended);
            Log($"disconnected: extended reason {extended}, description: {description}");
        });
    }

    private void MoveToSeparateWindow(bool useSetParent) {
        if (_separateWindow is not null) {
            Log("move: the session is already in a separate window");
            return;
        }
        var window = new SessionWindow();
        window.FormClosing += (_, _) => ReturnToMainWindow(closeWindow: false);
        window.DpiChanged += (_, e) => {
            Log($"separate window DPI {e.DeviceDpiOld} -> {e.DeviceDpiNew}");
            ScheduleDisplayUpdate(window, EventArgs.Empty);
        };
        window.Show();

        var handleBefore = _host.Handle;
        var ocxBefore = _host.CurrentOcx;
        if (useSetParent) {
            SetParent(_host.Handle, window.SessionPanel.Handle);
            _movedWithSetParent = true;
            FitHostTo(window.SessionPanel);
            window.SessionPanel.Resize += OnSeparatePanelResize;
        } else {
            _host.Parent = window.SessionPanel;
        }
        _separateWindow = window;
        LogMove(useSetParent ? "move (Win32 SetParent)" : "move (WinForms Parent)", handleBefore, ocxBefore);
    }

    private void ReturnToMainWindow(bool closeWindow) {
        var window = _separateWindow;
        if (window is null) {
            return;
        }
        _separateWindow = null;

        var handleBefore = _host.Handle;
        var ocxBefore = _host.CurrentOcx;
        if (_movedWithSetParent) {
            window.SessionPanel.Resize -= OnSeparatePanelResize;
            SetParent(_host.Handle, _sessionArea.Handle);
            _movedWithSetParent = false;
            FitHostTo(_sessionArea);
            _sessionArea.PerformLayout();
        } else {
            _host.Parent = _sessionArea;
        }
        LogMove("return", handleBefore, ocxBefore);
        if (closeWindow) {
            window.Close();
        }
    }

    private void LogMove(string action, IntPtr handleBefore, object? ocxBefore) {
        var handle = _host.Handle == handleBefore ? "kept" : "re-created";
        var ocx = ReferenceEquals(ocxBefore, _host.CurrentOcx) ? "same" : "new";
        Run(action, () => Log($"{action}: window handle {handle}, control instance {ocx}, Connected={_host.Client.Connected}"));
        ScheduleDisplayUpdate(this, EventArgs.Empty);
    }

    private void OnSeparatePanelResize(object? sender, EventArgs e) {
        if (sender is Control panel) {
            FitHostTo(panel);
        }
    }

    private void FitHostTo(Control parent) {
        GetClientRect(parent.Handle, out var rect);
        MoveWindow(_host.Handle, 0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top, true);
    }

    private void ScheduleDisplayUpdate(object? sender, EventArgs e) {
        if (!_autoApplyDisplay.Checked) {
            return;
        }
        _displayTimer.Stop();
        _displayTimer.Start();
    }

    private SessionDisplay CurrentDisplay() {
        GetClientRect(_host.Handle, out var rect);
        var dpi = (int)GetDpiForWindow(_host.Handle);
        return SessionDisplay.From(rect.Right - rect.Left, rect.Bottom - rect.Top, dpi, _passScale.Checked);
    }

    // Only the form of the server and user name goes to the log, never the values.
    private static string DescribeServer(string server) {
        if (server.Equals("localhost", StringComparison.OrdinalIgnoreCase)) {
            return "localhost";
        }
        return System.Net.IPAddress.TryParse(server, out _) ? "IP address" : "host name";
    }

    private static string DescribeUserName(string userName) {
        if (userName.Length == 0) {
            return "empty";
        }
        if (userName.StartsWith(@"MicrosoftAccount\", StringComparison.OrdinalIgnoreCase)) {
            return @"MicrosoftAccount\...";
        }
        if (userName.StartsWith(@".\", StringComparison.Ordinal)) {
            return @".\name";
        }
        if (userName.Contains('\\')) {
            return @"DOMAIN\name";
        }
        return userName.Contains('@') ? "name@domain" : "plain name";
    }

    private bool IsInsideSession(IntPtr hwnd) =>
        _host.IsHandleCreated && (hwnd == _host.Handle || IsChild(_host.Handle, hwnd));

    private void Set(string name, Action action) => Run($"set {name}", action);

    private void Run(string name, Action action) {
        try {
            action();
        } catch (Exception ex) {
            Log($"{name} failed: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}");
        }
    }

    private void Log(string text) {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {text}{Environment.NewLine}";
        if (IsHandleCreated) {
            BeginInvoke(() => _log.AppendText(line));
        } else {
            _log.AppendText(line);
        }
    }

    private static void AddSection(FlowLayoutPanel panel, string text) {
        panel.Controls.Add(new Label {
            Text = text,
            AutoSize = true,
            Font = new Font(panel.Font, FontStyle.Bold),
            Margin = new Padding(3, 12, 3, 2),
        });
    }

    private static void AddLabel(FlowLayoutPanel panel, string text) {
        panel.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
    }

    private static TextBox AddText(FlowLayoutPanel panel, string caption) {
        AddLabel(panel, caption);
        var box = new TextBox { Width = FieldWidth };
        panel.Controls.Add(box);
        return box;
    }

    private static CheckBox AddCheck(FlowLayoutPanel panel, string text, bool isChecked) {
        var box = new CheckBox { Text = text, Checked = isChecked, AutoSize = true };
        panel.Controls.Add(box);
        return box;
    }

    private static RadioButton AddRadio(FlowLayoutPanel panel, string text, bool isChecked) {
        var radio = new RadioButton { Text = text, Checked = isChecked, AutoSize = true };
        panel.Controls.Add(radio);
        return radio;
    }

    private static ComboBox AddCombo(FlowLayoutPanel panel, string caption, int selected, params string[] items) {
        AddLabel(panel, caption);
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = FieldWidth };
        combo.Items.AddRange(items);
        combo.SelectedIndex = selected;
        panel.Controls.Add(combo);
        return combo;
    }

    private void AddButton(FlowLayoutPanel panel, string text, Action action) {
        panel.Controls.Add(MakeButton(text, action, FieldWidth));
    }

    private Button MakeButton(string text, Action action, int width) {
        var button = new Button { Text = text, Width = width, Height = 28 };
        button.Click += (_, _) => Run(text, action);
        return button;
    }
}
