using System.Runtime.InteropServices;

namespace RemoteMe.RdpInterop;

// Receives the control's events over IDispatch and passes the ones the sessions need to the control.
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class RdpEventSink(RdpClientControl owner) : IMsTscAxEvents {
    public void OnConnecting() {
    }

    public void OnConnected() {
    }

    public void OnLoginComplete() => owner.RaiseLoginComplete();

    public void OnDisconnected(int discReason) => owner.RaiseDisconnected(discReason);

    public void OnEnterFullScreenMode() {
    }

    public void OnLeaveFullScreenMode() {
    }

    public void OnChannelReceivedData(string chanName, string data) {
    }

    public void OnRequestGoFullScreen() => owner.RaiseRequestGoFullScreen();

    public void OnRequestLeaveFullScreen() => owner.RaiseRequestLeaveFullScreen();

    public void OnFatalError(int errorCode) {
    }

    public void OnWarning(int warningCode) {
    }

    public void OnRemoteDesktopSizeChange(int width, int height) {
    }

    public void OnIdleTimeoutNotification() {
    }

    public void OnRequestContainerMinimize() {
    }

    public void OnConfirmClose(out bool allowClose) {
        allowClose = true;
    }

    public void OnReceivedTSPublicKey(string publicKey, out bool continueLogon) {
        continueLogon = true;
    }

    public void OnAutoReconnecting(int disconnectReason, int attemptCount, out AutoReconnectContinueState continueState) {
        continueState = AutoReconnectContinueState.Automatic;
    }

    public void OnAuthenticationWarningDisplayed() {
    }

    public void OnAuthenticationWarningDismissed() {
    }

    public void OnRemoteProgramResult(string remoteProgram, int error, bool isExecutable) {
    }

    public void OnRemoteProgramDisplayed(bool displayed, uint displayInformation) {
    }

    public void OnRemoteWindowDisplayed(bool displayed, object hwnd, int windowAttribute) {
    }

    public void OnLogonError(int error) => owner.RaiseLogonNotice(error);

    public void OnFocusReleased(int direction) {
    }

    public void OnUserNameAcquired(string userName) {
    }

    public void OnMouseInputModeChanged(bool mouseModeRelative) {
    }

    public void OnServiceMessageReceived(string serviceMessage) {
    }

    public void OnConnectionBarPullDown() {
    }

    public void OnNetworkStatusChanged(uint qualityLevel, int bandwidth, int rtt) {
    }

    public void OnDevicesButtonPressed() {
    }

    public void OnAutoReconnected() => owner.RaiseAutoReconnected();

    public void OnAutoReconnecting2(int disconnectReason, bool networkAvailable, int attemptCount, int maxAttemptCount) =>
        owner.RaiseAutoReconnecting();
}
