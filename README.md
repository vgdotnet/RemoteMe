# RemoteMe - a custom RDP client

A Windows application (.NET 10, WinForms) built on the standard RDP component that ships with Windows:
the remote desktop resolution follows the window size, the picture stays sharp at any display scale,
and the interface contains only the connection list and the session itself.

- Code: `C:\projects\RemoteMe`, https://github.com/vgdotnet/RemoteMe (branch main)
- Status as of 2026-11-13: requirements collected, no code yet
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
   the file.
4. **One encrypted settings file.** It holds all connections, is encrypted with the master password, and
   is moved between machines by copying. The key is derived from the password with PBKDF2 (many
   iterations, random salt), encryption is AES-256-GCM (a wrong password and a damaged file are detected
   immediately).
5. **Connection passwords are stored in the file.** Connecting takes one click, the Windows credentials
   dialog does not appear.
6. **Platform - modern .NET.** .NET 10 (SDK 10.0.404 is installed on DESKTOP-HOST), WinForms,
   `net10.0-windows`.
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

## Interface

Decisions made on 2026-11-12 and 2026-11-13.

- **No top bar.** No quick-connect field with protocol selection, no menu. Only two commands are needed
  from it: add a connection and change the master password.
- **Connection properties are a separate form.** It opens only when a connection is added or edited.
  There is no permanent properties panel. Fields: name, address, user name, password; anything else only
  if it turns out to be needed.
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
- Write the wrapper by hand: our own class derived from `AxHost` plus calls through `dynamic`. Then there
  are no separate interop DLLs and no need to generate them with `aximp.exe` (`dotnet build` cannot do
  that).
- Resolution on the fly: `UpdateSessionDisplaySettings` in the window's `ResizeEnd` (not in `Resize`).
- Sharpness: the application is PerMonitorV2, and the scale is passed to the session through the extended
  properties `DesktopScaleFactor` and `DeviceScaleFactor`.
- The connection password is passed to the component directly (`ClearTextPassword`), CredSSP is enabled.
- The RDP component itself leaves traces on the machine: server certificate thumbprints in the registry
  (`HKCU\Software\Microsoft\Terminal Server Client`) and a bitmap cache in the user profile. The cache
  can be turned off.

## To verify during implementation

- [ ] Whether the RDP component survives being moved from one window to another without reconnecting.
      Fallback: reconnect in 1-2 seconds, the session inside the VM stays alive.
- [ ] Whether single-file .NET extracts any libraries to a temporary folder.
- [ ] Which key combinations reach the application while the focus is inside the remote desktop.
- [ ] Changing the scale on the fly (moving a window between monitors with different scales): how
      programs inside the VM behave, and whether a reconnect is needed.
- [ ] Size of the final exe (CdRescue on the same platform is about 50 MB).

## Open questions

- Hotkeys: hide and show the list, next session, full screen.
- Where to put "Change master password" and "Log" (proposed: at the bottom of the list, next to "Add
  connection").
- Whether one session stretched across several monitors is needed (a separate RDP feature, full screen
  only).
- What to do if the settings file changes on disk while the application is open (copied from another
  machine).

## Findings along the way

- **PIN does not work in a Hyper-V enhanced session** by design: this mode runs over RDP. In the VM,
  turn off Settings - Accounts - Sign-in options - "only allow Windows Hello sign-in for Microsoft
  accounts" and sign in with a password.
- **mstsc** changes the resolution only when the window is maximized or switched to full screen, not when
  the window border is dragged (from memory, not verified).
