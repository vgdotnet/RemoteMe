using System.Runtime.InteropServices;

namespace RdpProbe.Interop;

// Declarations follow the type library embedded in %SystemRoot%\System32\mstscax.dll
// (dumped with prototypes/TypeLibDump). Dual interfaces are declared as dispatch-only, so only the
// members in use are listed and the vtable order does not matter. IUnknown-only interfaces must list
// every slot in order.

internal static class RdpClassIds {
    // The oldest coclass whose default interface is IMsRdpClient9 (UpdateSessionDisplaySettings).
    public const string MsRdpClient9NotSafeForScripting = "8b918b82-7985-4c24-89df-c33ad2bbfbcd";
}

internal enum ControlReconnectStatus {
    Started = 0,
    Blocked = 1,
}

internal enum AutoReconnectContinueState {
    Automatic = 0,
    Stop = 1,
    Manual = 2,
}

[ComImport]
[Guid("28904001-04b6-436c-a55b-0af1a0883dc9")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IMsRdpClient9 {
    [DispId(0x01)] string Server { get; set; }
    [DispId(0x02)] string Domain { get; set; }
    [DispId(0x03)] string UserName { get; set; }
    [DispId(0x06)] short Connected { get; }
    [DispId(0x0C)] int DesktopWidth { get; set; }
    [DispId(0x0D)] int DesktopHeight { get; set; }
    [DispId(0x15)] string Version { get; }
    [DispId(0x1E)] void Connect();
    [DispId(0x1F)] void Disconnect();
    [DispId(0x64)] int ColorDepth { get; set; }
    [DispId(0x67)] int ExtendedDisconnectReason { get; }
    [DispId(0x1F7)] string GetErrorDescription(uint disconnectReason, uint extendedDisconnectReason);
    [DispId(0x25A)] string GetStatusText(uint statusCode);
    [DispId(0x25B)] IMsRdpClientSecuredSettings2 SecuredSettings3 { get; }
    [DispId(0x2BD)] IMsRdpClientAdvancedSettings8 AdvancedSettings9 { get; }
    [DispId(0x2BE)] ControlReconnectStatus Reconnect(uint width, uint height);
    [DispId(0x321)] void SyncSessionDisplaySettings();

    [DispId(0x322)]
    void UpdateSessionDisplaySettings(
        uint desktopWidth,
        uint desktopHeight,
        uint physicalWidth,
        uint physicalHeight,
        uint orientation,
        uint desktopScaleFactor,
        uint deviceScaleFactor);
}

[ComImport]
[Guid("89acb528-2557-4d16-8625-226a30e97e9a")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IMsRdpClientAdvancedSettings8 {
    [DispId(0x11)] bool EnableCredSspSupport { get; set; }
    [DispId(0x6C)] int RDPPort { get; set; }
    [DispId(0x6F)] int EnableWindowsKey { get; set; }
    [DispId(0xAD)] int ContainerHandledFullScreen { get; set; }
    [DispId(0xB6)] int BitmapPersistence { get; set; }
    [DispId(0xB8)] bool SmartSizing { get; set; }
    [DispId(0xBA)] string ClearTextPassword { set; }
    [DispId(0xBB)] bool DisplayConnectionBar { get; set; }
    [DispId(0xBD)] bool GrabFocusOnConnect { get; set; }
    [DispId(0xBF)] bool RedirectDrives { get; set; }
    [DispId(0xC0)] bool RedirectPrinters { get; set; }
    [DispId(0xCC)] bool NotifyTSPublicKey { get; set; }
    [DispId(0xCD)] bool CanAutoReconnect { get; }
    [DispId(0xCE)] bool EnableAutoReconnect { get; set; }
    [DispId(0xCF)] int MaxReconnectAttempts { get; set; }
    [DispId(0xD4)] uint AuthenticationLevel { get; set; }
    [DispId(0xD5)] bool RedirectClipboard { get; set; }
    [DispId(0xD7)] uint AudioRedirectionMode { get; set; }
    [DispId(0xDD)] bool RelativeMouseMode { get; set; }
    [DispId(0xDE)] string AuthenticationServiceClass { get; set; }
    [DispId(0xDF)] string PCB { get; set; }
    [DispId(0xE0)] int HotKeyFocusReleaseLeft { get; set; }
    [DispId(0xE1)] int HotKeyFocusReleaseRight { get; set; }
    [DispId(0xE4)] bool AudioCaptureRedirectionMode { get; set; }
    [DispId(0xE8)] bool NegotiateSecurityLayer { get; set; }
}

[ComImport]
[Guid("25f2ce20-8b1d-4971-a7cd-549dae201fc0")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IMsRdpClientSecuredSettings2 {
    [DispId(0x04)] int KeyboardHookMode { get; set; }
    [DispId(0x05)] int AudioRedirectionMode { get; set; }
}

[ComImport]
[Guid("4f6996d5-d7b1-412c-b0ff-063718566907")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMsRdpClientNonScriptable5 {
    // IMsTscNonScriptable, slots 3-12
    void SetClearTextPassword([MarshalAs(UnmanagedType.BStr)] string value);
    void SetPortablePassword([MarshalAs(UnmanagedType.BStr)] string value);
    [return: MarshalAs(UnmanagedType.BStr)] string GetPortablePassword();
    void SetPortableSalt([MarshalAs(UnmanagedType.BStr)] string value);
    [return: MarshalAs(UnmanagedType.BStr)] string GetPortableSalt();
    void SetBinaryPassword([MarshalAs(UnmanagedType.BStr)] string value);
    [return: MarshalAs(UnmanagedType.BStr)] string GetBinaryPassword();
    void SetBinarySalt([MarshalAs(UnmanagedType.BStr)] string value);
    [return: MarshalAs(UnmanagedType.BStr)] string GetBinarySalt();
    void ResetPassword();

    // IMsRdpClientNonScriptable, slots 13-14
    void NotifyRedirectDeviceChange(nuint wParam, nint lParam);
    void SendKeys(int numKeys, IntPtr keyUpArray, IntPtr keyDataArray);

    // IMsRdpClientNonScriptable2, slots 15-16
    void SetUIParentWindowHandle(IntPtr value);
    IntPtr GetUIParentWindowHandle();

    // IMsRdpClientNonScriptable3, slots 17-36
    void SetShowRedirectionWarningDialog([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetShowRedirectionWarningDialog();
    void SetPromptForCredentials([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetPromptForCredentials();
    void SetNegotiateSecurityLayer([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetNegotiateSecurityLayer();
    void SetEnableCredSspSupport([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetEnableCredSspSupport();
    void SetRedirectDynamicDrives([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetRedirectDynamicDrives();
    void SetRedirectDynamicDevices([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetRedirectDynamicDevices();
    IntPtr GetDeviceCollection();
    IntPtr GetDriveCollection();
    void SetWarnAboutSendingCredentials([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetWarnAboutSendingCredentials();
    void SetWarnAboutClipboardRedirection([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetWarnAboutClipboardRedirection();
    void SetConnectionBarText([MarshalAs(UnmanagedType.BStr)] string value);
    [return: MarshalAs(UnmanagedType.BStr)] string GetConnectionBarText();

    // IMsRdpClientNonScriptable4, slots 37-52
    void SetRedirectionWarningType(int value);
    int GetRedirectionWarningType();
    void SetMarkRdpSettingsSecure([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetMarkRdpSettingsSecure();
    void SetPublisherCertificateChain(IntPtr value);
    void GetPublisherCertificateChain(IntPtr value);
    void SetWarnAboutPrinterRedirection([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetWarnAboutPrinterRedirection();
    void SetAllowCredentialSaving([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetAllowCredentialSaving();
    void SetPromptForCredsOnClient([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetPromptForCredsOnClient();
    void SetLaunchedViaClientShellInterface([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetLaunchedViaClientShellInterface();
    void SetTrustedZoneSite([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetTrustedZoneSite();

    // IMsRdpClientNonScriptable5, slots 53-64
    void SetUseMultimon([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetUseMultimon();
    uint GetRemoteMonitorCount();
    void GetRemoteMonitorsBoundingBox(out int left, out int top, out int right, out int bottom);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetRemoteMonitorLayoutMatchesLocal();
    void SetDisableConnectionBar([MarshalAs(UnmanagedType.VariantBool)] bool value);
    void SetDisableRemoteAppCapsCheck([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetDisableRemoteAppCapsCheck();
    void SetWarnAboutDirectXRedirection([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetWarnAboutDirectXRedirection();
    void SetAllowPromptingForCredentials([MarshalAs(UnmanagedType.VariantBool)] bool value);
    [return: MarshalAs(UnmanagedType.VariantBool)] bool GetAllowPromptingForCredentials();
}

[ComImport]
[Guid("302d8188-0052-4807-806a-362b628f9ac5")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMsRdpExtendedSettings {
    void SetProperty(
        [MarshalAs(UnmanagedType.BStr)] string name,
        [In, MarshalAs(UnmanagedType.Struct)] ref object value);

    [return: MarshalAs(UnmanagedType.Struct)]
    object GetProperty([MarshalAs(UnmanagedType.BStr)] string name);
}

[ComImport]
[Guid("336d5562-efa8-482e-8cb3-c5c0fc7a7db6")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IMsTscAxEvents {
    [DispId(0x01)] void OnConnecting();
    [DispId(0x02)] void OnConnected();
    [DispId(0x03)] void OnLoginComplete();
    [DispId(0x04)] void OnDisconnected(int discReason);
    [DispId(0x05)] void OnEnterFullScreenMode();
    [DispId(0x06)] void OnLeaveFullScreenMode();
    [DispId(0x07)] void OnChannelReceivedData(string chanName, string data);
    [DispId(0x08)] void OnRequestGoFullScreen();
    [DispId(0x09)] void OnRequestLeaveFullScreen();
    [DispId(0x0A)] void OnFatalError(int errorCode);
    [DispId(0x0B)] void OnWarning(int warningCode);
    [DispId(0x0C)] void OnRemoteDesktopSizeChange(int width, int height);
    [DispId(0x0D)] void OnIdleTimeoutNotification();
    [DispId(0x0E)] void OnRequestContainerMinimize();
    [DispId(0x0F)] void OnConfirmClose(out bool allowClose);
    [DispId(0x10)] void OnReceivedTSPublicKey(string publicKey, out bool continueLogon);
    [DispId(0x11)] void OnAutoReconnecting(int disconnectReason, int attemptCount, out AutoReconnectContinueState continueState);
    [DispId(0x12)] void OnAuthenticationWarningDisplayed();
    [DispId(0x13)] void OnAuthenticationWarningDismissed();
    [DispId(0x14)] void OnRemoteProgramResult(string remoteProgram, int error, bool isExecutable);
    [DispId(0x15)] void OnRemoteProgramDisplayed(bool displayed, uint displayInformation);
    [DispId(0x1D)] void OnRemoteWindowDisplayed(bool displayed, object hwnd, int windowAttribute);
    [DispId(0x16)] void OnLogonError(int error);
    [DispId(0x17)] void OnFocusReleased(int direction);
    [DispId(0x18)] void OnUserNameAcquired(string userName);
    [DispId(0x1A)] void OnMouseInputModeChanged(bool mouseModeRelative);
    [DispId(0x1C)] void OnServiceMessageReceived(string serviceMessage);
    [DispId(0x1E)] void OnConnectionBarPullDown();
    [DispId(0x20)] void OnNetworkStatusChanged(uint qualityLevel, int bandwidth, int rtt);
    [DispId(0x23)] void OnDevicesButtonPressed();
    [DispId(0x21)] void OnAutoReconnected();
    [DispId(0x22)] void OnAutoReconnecting2(int disconnectReason, bool networkAvailable, int attemptCount, int maxAttemptCount);
}
