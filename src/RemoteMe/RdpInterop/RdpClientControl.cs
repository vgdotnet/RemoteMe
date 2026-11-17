using System.ComponentModel;
using System.Runtime.InteropServices;
using RemoteMe.Core.Sessions;
using static RemoteMe.NativeMethods;

namespace RemoteMe.RdpInterop;

// The RDP ActiveX control (mstscax.dll) hosted without generated interop assemblies. It follows its
// own size and DPI: 300 ms after the last change it sends the new desktop size and scale to the session.
[DesignerCategory("Code")]
internal sealed class RdpClientControl : AxHost, IRdpClient {
    private const uint WarnIfServerAuthenticationFails = 2;
    private const uint AudioPlayOnThisComputer = 0;
    private const uint AudioDoNotPlay = 2;
    private const int MinDisplayUpdateSize = 100;
    private const int RdpPort = 3389;
    private const string RdpServiceClass = "TERMSRV";
    private const int HyperVPort = 2179;
    private const string HyperVServiceClass = "Microsoft Virtual Console Service";

    private readonly RdpEventSink _sink;
    private readonly System.Windows.Forms.Timer _displayTimer = new() { Interval = 300 };
    private ConnectionPointCookie? _cookie;
    private bool _signedIn;
    private bool _fixedDisplay;
    private SessionDisplay? _appliedDisplay;

    public RdpClientControl() : base(RdpClassIds.MsRdpClient9NotSafeForScripting) {
        _sink = new RdpEventSink(this);
        _displayTimer.Tick += (_, _) => {
            _displayTimer.Stop();
            ApplyDisplay();
        };
    }

    public event EventHandler? LoginComplete;

    public event EventHandler? AutoReconnecting;

    public event EventHandler? AutoReconnected;

    public event EventHandler<RdpDisconnectedEventArgs>? Disconnected;

    public event EventHandler<RdpLogonNoticeEventArgs>? LogonNotice;

    public event EventHandler<string>? Warning;

    // The component asks its container to enter or leave full screen (ContainerHandledFullScreen).
    public event EventHandler? RequestGoFullScreen;

    public event EventHandler? RequestLeaveFullScreen;

    private object Ocx => GetOcx() ?? throw new InvalidOperationException("The RDP control is not created yet.");

    private IMsRdpClient9 Client => (IMsRdpClient9)Ocx;

    public void Connect(RdpConnectRequest request) {
        var client = Client;
        var advanced = client.AdvancedSettings9;
        var nonScriptable = (IMsRdpClientNonScriptable5)Ocx;
        var display = CurrentDisplay();
        var hyperV = request.HyperV;
        _appliedDisplay = display;
        // A VM's console keeps the guest's own resolution; the component refuses to change it.
        _fixedDisplay = hyperV is { EnhancedSession: false };

        client.Server = request.Server;
        client.UserName = request.UserName;
        client.Domain = "";
        client.DesktopWidth = (int)display.Width;
        client.DesktopHeight = (int)display.Height;
        client.ColorDepth = 32;
        advanced.EnableCredSspSupport = true;
        advanced.AuthenticationLevel = WarnIfServerAuthenticationFails;
        // Every property is set each time, since the control is reused when a connection is edited.
        // The Hyper-V values are the ones the stage 1 probe connected with (see the README).
        advanced.RDPPort = hyperV is null ? RdpPort : HyperVPort;
        advanced.PCB = hyperV is null ? "" : hyperV.VmId.ToString() + (hyperV.EnhancedSession ? ";EnhancedMode=1" : "");
        advanced.AuthenticationServiceClass = hyperV is null ? RdpServiceClass : HyperVServiceClass;
        advanced.NegotiateSecurityLayer = hyperV is null;
        SetExtendedProperty("DisableCredentialsDelegation", hyperV is not null);
        advanced.EnableAutoReconnect = true;
        advanced.MaxReconnectAttempts = 20;
        advanced.BitmapPersistence = 0;
        advanced.SmartSizing = false;
        advanced.GrabFocusOnConnect = true;
        advanced.ContainerHandledFullScreen = 1;
        advanced.DisplayConnectionBar = false;
        advanced.RedirectClipboard = request.Redirection.Clipboard;
        advanced.RedirectDrives = request.Redirection.Drives;
        advanced.RedirectPrinters = request.Redirection.Printers;
        advanced.AudioRedirectionMode = request.Redirection.Audio ? AudioPlayOnThisComputer : AudioDoNotPlay;
        advanced.AudioCaptureRedirectionMode = request.Redirection.Microphone;
        // The component passes the credentials it has to the Hyper-V host, so a VM in an enhanced session
        // asks for its own password in a Windows dialog (decided on 2026-11-17). Nothing is remembered.
        nonScriptable.SetAllowPromptingForCredentials(hyperV is { EnhancedSession: true });
        nonScriptable.SetAllowCredentialSaving(false);
        SetExtendedProperty("DesktopScaleFactor", display.DesktopScaleFactor);
        SetExtendedProperty("DeviceScaleFactor", display.DeviceScaleFactor);
        advanced.ClearTextPassword = request.Password?.Reveal() ?? "";

        client.Connect();
    }

    public void Disconnect() {
        try {
            if (Client.Connected != 0) {
                Client.Disconnect();
                return;
            }
        } catch (COMException) {
        }
        // Nothing was connected, so the component raises no event; report it here for the session.
        RaiseDisconnected(1);
    }

    // Asks the component to enter or leave full screen; it answers with RequestGoFullScreen or
    // RequestLeaveFullScreen. Returns false when no answer will come (already in that mode, or refused).
    public bool TrySetFullScreen(bool fullScreen) {
        try {
            if (Client.FullScreen == fullScreen) {
                return false;
            }
            Client.FullScreen = fullScreen;
            return true;
        } catch (COMException) {
            return false;
        }
    }

    // The physical Ctrl+Alt+Del always acts on the local computer. Scan codes: Ctrl 0x1D, Alt 0x38,
    // Del 0x53, pressed in that order and released in reverse; the component wants the focus for it.
    public void SendCtrlAltDel() {
        int[] keys = [0x1D, 0x38, 0x53, 0x53, 0x38, 0x1D];
        short[] keyUp = [0, 0, 0, -1, -1, -1];
        try {
            Focus();
            ((IMsRdpClientNonScriptable5)Ocx).SendKeys(keys.Length, keyUp, keys);
        } catch (COMException ex) {
            Warning?.Invoke(this, $"Ctrl+Alt+Del could not be sent: {ex.Message}");
        }
    }

    internal void RaiseLoginComplete() {
        _signedIn = true;
        ScheduleDisplayUpdate();
        LoginComplete?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseAutoReconnecting() => AutoReconnecting?.Invoke(this, EventArgs.Empty);

    internal void RaiseAutoReconnected() => AutoReconnected?.Invoke(this, EventArgs.Empty);

    internal void RaiseLogonNotice(int code) => LogonNotice?.Invoke(this, new RdpLogonNoticeEventArgs(code));

    internal void RaiseRequestGoFullScreen() => RequestGoFullScreen?.Invoke(this, EventArgs.Empty);

    internal void RaiseRequestLeaveFullScreen() => RequestLeaveFullScreen?.Invoke(this, EventArgs.Empty);

    internal void RaiseDisconnected(int reason) {
        _signedIn = false;
        var extended = 0;
        var text = "";
        try {
            extended = Client.ExtendedDisconnectReason;
            text = Client.GetErrorDescription((uint)reason, (uint)extended);
        } catch (COMException) {
        }
        Disconnected?.Invoke(this, new RdpDisconnectedEventArgs(reason, extended, text));
    }

    protected override void CreateSink() {
        _cookie = new ConnectionPointCookie(Ocx, _sink, typeof(IMsTscAxEvents));
    }

    protected override void DetachSink() {
        _cookie?.Disconnect();
        _cookie = null;
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        ScheduleDisplayUpdate();
    }

    protected override void OnVisibleChanged(EventArgs e) {
        base.OnVisibleChanged(e);
        ScheduleDisplayUpdate();
    }

    protected override void OnParentChanged(EventArgs e) {
        base.OnParentChanged(e);
        ScheduleDisplayUpdate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) {
        base.OnDpiChangedAfterParent(e);
        ScheduleDisplayUpdate();
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _displayTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ScheduleDisplayUpdate() {
        if (!_signedIn || _fixedDisplay) {
            return;
        }
        _displayTimer.Stop();
        _displayTimer.Start();
    }

    private void ApplyDisplay() {
        if (!_signedIn || !IsHandleCreated || !Visible || FindForm() is not { WindowState: not FormWindowState.Minimized }) {
            return;
        }
        if (ClientSize.Width < MinDisplayUpdateSize || ClientSize.Height < MinDisplayUpdateSize) {
            return;
        }
        var display = CurrentDisplay();
        if (display == _appliedDisplay) {
            return;
        }
        try {
            Client.UpdateSessionDisplaySettings(
                display.Width,
                display.Height,
                display.PhysicalWidthMm,
                display.PhysicalHeightMm,
                0,
                display.DesktopScaleFactor,
                display.DeviceScaleFactor);
            _appliedDisplay = display;
        } catch (COMException ex) {
            Warning?.Invoke(this, $"The remote desktop size could not be changed: {ex.Message}");
        }
    }

    // GetDpiForWindow rather than DeviceDpi: it stays right after the control moves to another window.
    private SessionDisplay CurrentDisplay() {
        var dpi = IsHandleCreated ? (int)GetDpiForWindow(Handle) : DeviceDpi;
        return SessionDisplay.From(ClientSize.Width, ClientSize.Height, dpi);
    }

    private void SetExtendedProperty(string name, object value) {
        ((IMsRdpExtendedSettings)Ocx).SetProperty(name, ref value);
    }
}
