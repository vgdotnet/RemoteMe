using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace RdpProbe.Interop;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class RdpEventSink : IMsTscAxEvents {
    private readonly Action<string> _log;

    public RdpEventSink(Action<string> log) {
        _log = log;
    }

    public Func<string, bool> AcceptPublicKey { get; set; } = _ => true;

    public event Action? Connected;
    public event Action? LoginComplete;
    public event Action<int>? Disconnected;

    public void OnConnecting() => _log("event OnConnecting");

    public void OnConnected() {
        _log("event OnConnected");
        Connected?.Invoke();
    }

    public void OnLoginComplete() {
        _log("event OnLoginComplete");
        LoginComplete?.Invoke();
    }

    public void OnDisconnected(int discReason) {
        _log($"event OnDisconnected discReason=0x{discReason:X} ({discReason})");
        Disconnected?.Invoke(discReason);
    }

    public void OnEnterFullScreenMode() => _log("event OnEnterFullScreenMode");

    public void OnLeaveFullScreenMode() => _log("event OnLeaveFullScreenMode");

    public void OnChannelReceivedData(string chanName, string data) => _log($"event OnChannelReceivedData channel={chanName}");

    public void OnRequestGoFullScreen() => _log("event OnRequestGoFullScreen");

    public void OnRequestLeaveFullScreen() => _log("event OnRequestLeaveFullScreen");

    public void OnFatalError(int errorCode) => _log($"event OnFatalError errorCode={errorCode}");

    public void OnWarning(int warningCode) => _log($"event OnWarning warningCode={warningCode}");

    public void OnRemoteDesktopSizeChange(int width, int height) => _log($"event OnRemoteDesktopSizeChange {width}x{height}");

    public void OnIdleTimeoutNotification() => _log("event OnIdleTimeoutNotification");

    public void OnRequestContainerMinimize() => _log("event OnRequestContainerMinimize");

    public void OnConfirmClose(out bool allowClose) {
        _log("event OnConfirmClose");
        allowClose = true;
    }

    public void OnReceivedTSPublicKey(string publicKey, out bool continueLogon) {
        // The key arrives as binary data packed into a BSTR; the hash only identifies it.
        var bytes = Encoding.Unicode.GetBytes(publicKey);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        continueLogon = AcceptPublicKey(hash);
        _log($"event OnReceivedTSPublicKey length={bytes.Length} bytes sha256={hash} continueLogon={continueLogon}");
    }

    public void OnAutoReconnecting(int disconnectReason, int attemptCount, out AutoReconnectContinueState continueState) {
        continueState = AutoReconnectContinueState.Automatic;
        _log($"event OnAutoReconnecting reason=0x{disconnectReason:X} attempt={attemptCount} -> {continueState}");
    }

    public void OnAuthenticationWarningDisplayed() => _log("event OnAuthenticationWarningDisplayed");

    public void OnAuthenticationWarningDismissed() => _log("event OnAuthenticationWarningDismissed");

    public void OnRemoteProgramResult(string remoteProgram, int error, bool isExecutable) =>
        _log($"event OnRemoteProgramResult error={error}");

    public void OnRemoteProgramDisplayed(bool displayed, uint displayInformation) =>
        _log($"event OnRemoteProgramDisplayed displayed={displayed}");

    public void OnRemoteWindowDisplayed(bool displayed, object hwnd, int windowAttribute) =>
        _log($"event OnRemoteWindowDisplayed displayed={displayed}");

    public void OnLogonError(int error) => _log($"event OnLogonError error=0x{error:X} ({error})");

    public void OnFocusReleased(int direction) => _log($"event OnFocusReleased direction={direction}");

    public void OnUserNameAcquired(string userName) => _log("event OnUserNameAcquired");

    public void OnMouseInputModeChanged(bool mouseModeRelative) => _log($"event OnMouseInputModeChanged relative={mouseModeRelative}");

    public void OnServiceMessageReceived(string serviceMessage) => _log("event OnServiceMessageReceived");

    public void OnConnectionBarPullDown() => _log("event OnConnectionBarPullDown");

    public void OnNetworkStatusChanged(uint qualityLevel, int bandwidth, int rtt) =>
        _log($"event OnNetworkStatusChanged quality={qualityLevel} bandwidth={bandwidth} rtt={rtt}");

    public void OnDevicesButtonPressed() => _log("event OnDevicesButtonPressed");

    public void OnAutoReconnected() => _log("event OnAutoReconnected");

    public void OnAutoReconnecting2(int disconnectReason, bool networkAvailable, int attemptCount, int maxAttemptCount) =>
        _log($"event OnAutoReconnecting2 reason=0x{disconnectReason:X} networkAvailable={networkAvailable} attempt={attemptCount}/{maxAttemptCount}");
}
