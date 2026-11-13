# RemoteMe - a custom RDP client

A Windows application (.NET 10, WinForms) built on the standard RDP component that ships with Windows:
the remote desktop resolution follows the window size, the picture stays sharp at any display scale,
and the interface contains only the connection list and the session itself.

- Code: `C:\projects\RemoteMe`, https://github.com/vgdotnet/RemoteMe (branch main)
- Status as of 2026-11-13: stage 1 (prototypes) done, stage 2 (vault and model) in progress
- Architecture: [docs/architecture.md](docs/architecture.md)
- Name: as far as we remember, RemoteMe is also the name of an IoT platform (remoteme.org) - this only
  matters when publishing

## Requirements

1. **Single exe.** All required DLLs are inside (self-contained single-file, Windows x64). The RDP engine
   itself (`mstscax.dll`) is part of Windows and is not included in the exe.
2. **Portable.** No installation, nothing of our own in the registry or in the user profile. The settings
   file lives next to the exe.
3. **Master password on startup.** It decrypts the settings file. The password is not stored anywhere,
   it is kept only in memory while the application is open. A forgotten password cannot be recovered.
   First run: there is no file yet, so the application asks for a new password (twice) and creates
   the file. There is no minimum length: a password shorter than 12 characters only gets a warning
   (decided on 2026-11-13). An empty password is not accepted.
4. **One encrypted settings file.** It holds all connections, is encrypted with the master password, and
   is moved between machines by copying. The key is derived from the password with PBKDF2 (many
   iterations, random salt), encryption is AES-256-GCM (a wrong password and a damaged file are detected
   immediately).
5. **Connection passwords are stored in the file.** Connecting takes one click, the Windows credentials
   dialog does not appear.
6. **Platform - modern .NET.** .NET 10 (SDK 10.0.404), WinForms, `net10.0-windows`.
7. **Sessions can be moved to separate windows.** This lets different sessions sit on different monitors
   at the same time. Commands: "Open in separate window" and "Return to main window". Each window has
   its own resolution, and the scale is recalculated when the window moves to a monitor with a different
   scale. There is still one list, in the main window; clicking a detached session brings its window to
   the front.
8. **Full screen on the current monitor.** Any session (in the main window or detached) can be expanded
   to the full screen of the monitor it is currently on. A hotkey returns it to the window.
9. **High-resolution monitors and scaling.** The application works on high-resolution monitors, and the
   scale selected in Windows for the monitor (125%, 150%, etc.) is passed to the RDP session. When the
   window moves to a monitor with a different scale, the session receives the new scale.
10. **No auto-lock.** Once unlocked, the application stays unlocked until it is closed.
11. **Single instance.** Only one running instance of the application per machine.
12. **Server certificates: remember after first consent.** When a server certificate is not trusted, the
    application warns once. After the user accepts it, the choice is remembered and the warning does not
    appear again for this server.
13. **Redirection is configured per connection.** Each connection has its own redirection settings
    (clipboard, drives, audio, microphone, printers). Defaults for a new connection (decided on
    2026-11-13): clipboard, audio and microphone on, drives and printers off.
14. **Direct Hyper-V connections.** A VM can be reached through its Hyper-V host, the way the Hyper-V
    console does it, without network access inside the VM.
15. **Reconnect on connection loss.** A session reconnects automatically when the connection drops.
    Open sessions are not restored on the next start.
16. **Window layout is not remembered.** The application does not store which session was on which
    monitor.

Decided for the first version:

- Key derivation is PBKDF2 (built in, no dependencies), not Argon2id.
- RD Gateway, non-standard ports and domain accounts are out of scope (probably not needed).
- Target systems: Windows 10 22H2 and Windows 11, x64.

## Interface

Decisions made on 2026-11-12 and 2026-11-13.

- **No top bar.** No quick-connect field with protocol selection, no menu. Only two commands are needed
  from it: add a connection and change the master password.
- **Connection properties are a separate form.** It opens only when a connection is added or edited.
  There is no permanent properties panel. Fields: name, connection type (network address, or a VM on a
  Hyper-V host), address, user name, password, redirection settings; anything else only if it turns out
  to be needed.
- **Connection list on the left.** Groups without a root node and without tree lines (a section caption
  with a collapse arrow; while there are no groups, a flat list). Two lines per connection: the name, and
  below it the user name and address in gray. Status dot: green - connected, empty circle - not
  connected, red - error. The whole row of the session currently on screen is highlighted. Actions
  (edit, disconnect, delete) are available from a three-dot button on hover and from the right-click
  menu. At the bottom there is one labeled button, "Add connection".
- **No tabs.** The list replaces them: clicking a connected row switches to its session, clicking a
  disconnected row connects.
- **No bottom notification bar.** The disconnect reason and the connection progress are shown in the
  session area (with a "Reconnect" button). Settings file errors are shown in a message box. A log of
  all messages is kept in memory and opens in a separate window on demand; it is not written to disk.
- **Session area** (proposed, no objections, not explicitly confirmed):
  - the resolution follows the window, with no scrollbars and no stretching;
  - the list can be hidden with one key or button, so the session takes the whole window.

## Technical notes

- The component is the Microsoft RDP Client ActiveX control (`mstscax.dll`), the same one that mstsc uses.
- Write the wrapper by hand: our own class derived from `AxHost` plus hand-declared COM interfaces. Then
  there are no separate interop DLLs and no need to generate them with `aximp.exe` (`dotnet build`
  cannot do that). The declarations come from the type library inside `mstscax.dll` (see
  [prototypes](prototypes/README.md)). Dual interfaces are declared dispatch-only with their DISPIDs, so
  only the members in use are listed; IUnknown-only interfaces (`IMsRdpClientNonScriptable5`,
  `IMsRdpExtendedSettings`) list every vtable slot in order. The coclass is
  `MsRdpClient9NotSafeForScripting`, the oldest one with `UpdateSessionDisplaySettings`.
- Resolution on the fly: `UpdateSessionDisplaySettings` once the size or DPI stops changing. The probe
  calls it 300 ms after the last change, which covers dragging the border, maximizing and moving to
  another monitor; the remote desktop follows in about 30 ms.
- Sharpness: the application is PerMonitorV2, and the scale is passed to the session through the extended
  properties `DesktopScaleFactor` and `DeviceScaleFactor`.
- The connection password is passed to the component directly (`ClearTextPassword`), CredSSP is enabled.
- The RDP component itself leaves traces on the machine: server certificate thumbprints in the registry
  (`HKCU\Software\Microsoft\Terminal Server Client\Servers\<server>\CertHash`, shared with mstsc) and a
  bitmap cache in the user profile. The cache is turned off with `BitmapPersistence = 0`; no cache files
  were written during the probe sessions.
- The component's input window (`IHWindowClass`) and output windows (`OPContainerClass`,
  `OPWindowClass`) run on a thread of their own. Keys typed into the session never pass through the
  WinForms message loop; only a low-level keyboard hook sees them.
- `GetErrorDescription` is not reliable: for a disconnect the user started (reason 1) it returns "An
  internal error has occurred". Disconnect texts for common reasons have to be our own.

## Build

Requires the .NET 10 SDK on Windows (`global.json` pins 10.0.404 and allows newer feature bands).

```
dotnet build RemoteMe.slnx
dotnet test RemoteMe.slnx
dotnet format RemoteMe.slnx --verify-no-changes
dotnet publish src/RemoteMe/RemoteMe.csproj -p:PublishProfile=SingleFile
```

The last command produces one self-contained `src/RemoteMe/bin/publish/RemoteMe.exe` (win-x64).

## To verify during implementation

- [x] Whether the RDP component survives being moved from one window to another without reconnecting.
      Fallback: reconnect in 1-2 seconds, the session inside the VM stays alive.
      Checked on 2026-11-13 with RdpProbe and a VM over the network: it survives. Moving the control to
      another form with the WinForms `Parent` property keeps its window and instance, the session goes on,
      and size and scale follow the new window, including a move to a monitor with another scale. A plain
      Win32 `SetParent` behind WinForms' back does not work (the control keeps the old panel's size and
      DPI) and is not needed.
- [x] Whether single-file .NET extracts any libraries to a temporary folder.
      Checked on 2026-11-13 with the stage 0 skeleton (an empty window, no RDP component yet): it does
      not. The publish folder holds only `RemoteMe.exe`, and on start nothing appears in
      `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, in `%TEMP%\.net` or next to the exe, with or without
      compression. Recheck once the RDP wrapper is in; it should add no files, since `mstscax.dll` is
      loaded from Windows.
- [ ] Which key combinations reach the application while the focus is inside the remote desktop.
      So far: the WinForms message loop sees none of them (see the technical notes), a low-level hook
      does. With the default keyboard hook mode (2, full screen only) Alt+Tab and Ctrl+Esc act on the
      local computer in a window. The remaining checks (modes 0 and 1, and whether our hook can take a
      combination away from the session) are moved to stage 5, where the hotkeys are made.
- [x] Changing the scale on the fly (moving a window between monitors with different scales): how
      programs inside the VM behave, and whether a reconnect is needed.
      Checked on 2026-11-13, 150% and 125% monitors: no reconnect is needed. `UpdateSessionDisplaySettings`
      with the new `DesktopScaleFactor` and `DeviceScaleFactor` applies within about 30 ms. The owner saw
      no problems in the guest; individual programs were not checked one by one.
- [ ] Direct Hyper-V connection: from memory, the Hyper-V console connects to port 2179 on the host and
      passes the VM ID in the pre-connection blob. Verify this, the rights required on the host, and
      which credentials are used for the host and for the guest.
      So far (2026-11-13, a local Hyper-V host): with a typed password and the service class
      `Microsoft Virtual Console Service` the connection stops at once with 0xF08, "credentials may not
      be sent to the remote computer" (from memory, the default CredSSP policy delegates typed
      credentials only to `TERMSRV/*`). The extended property `DisableCredentialsDelegation = true`
      removes this error. With a typed password the host then refused (0x904 with extended reason 7, or
      0x807). What works for a basic session: server `localhost`, port 2179, the VM ID in `PCB`, the
      service class above, CredSSP on, `NegotiateSecurityLayer` off, `DisableCredentialsDelegation` on,
      and an empty user name and password, so the current Windows account is used (the account was in
      the Hyper-V Administrators group); the host's IP address works as well as `localhost`. A user
      name and password set on the component go to the host, not to the guest: the guest's
      credentials were refused with 0x807. The component warns about the host's certificate. A basic
      session shows the VM console at the guest's own resolution (1024x768 in the test);
      `UpdateSessionDisplaySettings` fails there with `E_UNEXPECTED`, and the guest's sign-in screen
      needs Ctrl+Alt+Del, which the physical keys send to the local computer. An enhanced session
      (`PCB` ending in `;EnhancedMode=1`) follows the window size, but asks for the guest password in a
      Windows credentials dialog; with prompts disallowed it stops after `OnConnected` and never signs
      in. Still open, moved to stage 6: passing the guest credentials without that dialog
      (requirement 5), sending Ctrl+Alt+Del in a basic session, and a remote Hyper-V host.
- [x] Server certificates: whether the component exposes the certificate thumbprint, so the consent can
      be stored in the settings file and travel between machines. Fallback: the component's own "do not
      ask again" option, which Windows stores in the registry on each machine separately.
      Checked on 2026-11-13: it does not. The type library has no certificate property, and
      `OnReceivedTSPublicKey` (with `NotifyTSPublicKey` on) was not raised in six connections with NLA;
      from memory, it belongs to the old RDP security layer. Unless we check the certificate ourselves
      (see open questions), the fallback applies. Consent given in mstsc is shared with the component.
- [x] Automatic reconnect: what the component does by itself and what we have to handle.
      Checked on 2026-11-13: with `EnableAutoReconnect` the component reconnects by itself after a
      network loss (`OnAutoReconnecting2`, then `OnAutoReconnected` and `OnLoginComplete`; 19 seconds in
      the test). While it reconnects, the component draws its own "reconnecting" message over the
      session. It does not reconnect after the guest restarts (`OnDisconnected` 0x3, extended reason 26).
      We handle the disconnects it gives up on and the texts.
- [x] Size of the final exe (CdRescue on the same platform is about 50 MB).
      Stage 0 skeleton: 46.9 MB with `EnableCompressionInSingleFile`, 102.1 MB without. Warm start to
      the first window is about 200 ms compressed and about 135 ms uncompressed. Compression is on.

## Open questions

- Hotkeys: hide and show the list, next session, full screen.
- Where to put "Change master password" and "Log" (proposed: at the bottom of the list, next to "Add
  connection").
- Whether one session stretched across several monitors is needed (a separate RDP feature, full screen
  only).
- What to do if the settings file changes on disk while the application is open (copied from another
  machine).
- Server certificates (requirement 12): accept the per-machine fallback, or read the certificate
  ourselves before connecting and keep the consent in the settings file. Decide before stage 6.
- Hyper-V guests: the component takes no separate guest credentials, so an enhanced session asks for
  the guest password in a Windows dialog, and requirement 5 is not met for such connections. Accept
  the dialog for them, or look further (for example, credentials saved in Windows for the time of a
  connection, not verified). Decide in stage 6.

## Findings along the way

- **PIN does not work in a Hyper-V enhanced session** by design: this mode runs over RDP. In the VM,
  turn off Settings - Accounts - Sign-in options - "only allow Windows Hello sign-in for Microsoft
  accounts" and sign in with a password.
- **mstsc** changes the resolution only when the window is maximized or switched to full screen, not when
  the window border is dragged (from memory, not verified).
