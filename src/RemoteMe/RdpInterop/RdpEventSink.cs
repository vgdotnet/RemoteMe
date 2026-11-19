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

    public void OnLoginComplete() => Guard(() => owner.RaiseLoginComplete());

    public void OnDisconnected(int discReason) => Guard(() => owner.RaiseDisconnected(discReason));

    public void OnEnterFullScreenMode() {
    }

    public void OnLeaveFullScreenMode() {
    }

    public void OnChannelReceivedData(string chanName, string data) {
    }

    public void OnRequestGoFullScreen() => Guard(() => owner.RaiseRequestGoFullScreen());

    public void OnRequestLeaveFullScreen() => Guard(() => owner.RaiseRequestLeaveFullScreen());

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

    public void OnLogonError(int error) => Guard(() => owner.RaiseLogonNotice(error));

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

    public void OnAutoReconnected() => Guard(() => owner.RaiseAutoReconnected());

    public void OnAutoReconnecting2(int disconnectReason, bool networkAvailable, int attemptCount, int maxAttemptCount) =>
        Guard(() => owner.RaiseAutoReconnecting());

    // An exception thrown back into the component would become an error code there and be lost, so it
    // goes to the application's handler, which writes it to the log and shows it.
    private static void Guard(Action raise) {
        try {
            raise();
        } catch (Exception ex) {
            Application.OnThreadException(ex);
        }
    }
}
