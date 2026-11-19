# RemoteMe - a custom RDP client

A Windows application (.NET 10, WinForms) built on the standard RDP component that ships with Windows:
the remote desktop resolution follows the window size, the picture stays sharp at any display scale,
and the interface contains only the connection list and the session itself.

- Download: [RemoteMe.exe](https://github.com/vgdotnet/RemoteMe/releases/latest/download/RemoteMe.exe)
  from the [latest release](https://github.com/vgdotnet/RemoteMe/releases/latest). No installation: put
  the exe in any folder, and the settings file is created next to it. To check the download, see
  "Build".
- Code: `C:\projects\RemoteMe`, https://github.com/vgdotnet/RemoteMe (branch main)
- Status as of 2026-11-19: stages 0-7 done, v0.7.0 released (an About window in the "..." menu with the
  version and its commit, the GPL notices, links and the details for a problem report; the compact
  strip shows the time of the clock; v0.6.0 added a doodle background in the left panel, in one of six
  colors chosen with "Background" in the "..." menu, with the clock text glowing white over it, and
  let the list buttons fill their bar without margins; v0.5.0 added a compact strip of circles when the
  list is dragged narrower than the clock, kept collapsed groups in the settings file, showed the clock
  as the date, the time, then the city with its UTC offset, let the full list get as narrow as the
  clock allows and made the selected and the hovered row fill the whole row; v0.4.0 made a click on a row only select
  it and a double click or Enter connect, with the group captions in the accent color of the Windows
  settings and connection names in bold; v0.3.1 fixed the clock clicks, the time zone search and the button colors; v0.3.0 added
  a clock with a chosen time zone above the list buttons; v0.2.0 added full screen with the list, a
  livelier connection list and the application icon); releases are not signed (see the code signing
  policy)
- Architecture: [docs/architecture.md](docs/architecture.md)
- Name: as far as we remember, RemoteMe is also the name of an IoT platform (remoteme.org); the name is
  kept (decided on 2026-11-17)

## Requirements

1. **Single exe.** All required DLLs are inside (self-contained single-file, Windows x64). The RDP engine
   itself (`mstscax.dll`) is part of Windows and is not included in the exe.
2. **Portable.** No installation, nothing of our own in the registry or in the user profile. The settings
   file, `RemoteMe.settings`, lives next to the exe.
3. **Master password on startup.** It decrypts the settings file. The password is not stored anywhere,
   it is kept only in memory while the application is open. A forgotten password cannot be recovered.
   First run: there is no file yet, so the application asks for a new password (twice) and creates
   the file. There is no minimum length: a password shorter than 12 characters only gets a warning
   (decided on 2026-11-13). An empty password is not accepted.
4. **One encrypted settings file.** It holds all connections, is encrypted with the master password, and
   is moved between machines by copying. The key is derived from the password with PBKDF2 (many
   iterations, random salt), encryption is AES-256-GCM (a wrong password and a damaged file are detected
   immediately).
5. **Connection passwords are stored in the file.** Connecting does not stop at the Windows
   credentials dialog. When a connection has no saved password, RemoteMe asks for it in its own
   window before connecting and offers to save it (decided on 2026-11-13). The exception is a Hyper-V
   VM in an enhanced session: its own password is asked in a Windows dialog (see requirement 14).
6. **Platform - modern .NET.** .NET 10 (SDK 10.0.404), WinForms, `net10.0-windows`.
7. **Sessions can be moved to separate windows.** This lets different sessions sit on different monitors
   at the same time. Commands: "Open in separate window" and "Return to main window". Each window has
   its own resolution, and the scale is recalculated when the window moves to a monitor with a different
   scale. There is still one list, in the main window; clicking a detached session brings its window to
   the front. Both commands are in the connection's menu. Closing a separate window disconnects its
   session (decided on 2026-11-13).
8. **Full screen on the current monitor.** Any session (in the main window or detached) can be expanded
   to the full screen of the monitor it is currently on. A hotkey returns it to the window. The hotkey
   is Ctrl+Alt+Break both ways, as in mstsc; "Full screen" is also in the connection's menu.
9. **High-resolution monitors and scaling.** The application works on high-resolution monitors, and the
   scale selected in Windows for the monitor (125%, 150%, etc.) is passed to the RDP session. When the
   window moves to a monitor with a different scale, the session receives the new scale.
10. **No auto-lock.** Once unlocked, the application stays unlocked until it is closed.
11. **Single instance.** Only one running instance of the application per machine.
12. **Server certificates: remember after first consent.** When a server certificate is not trusted, the
    application warns once. After the user accepts it, the choice is remembered and the warning does not
    appear again for this server. The warning is the RDP component's own dialog with its "Don't ask me
    again for connections to this computer" option; Windows keeps that consent in the registry of each
    machine, shared with mstsc, so on another machine it is asked once more (decided on 2026-11-17).
13. **Redirection is configured per connection.** Each connection has its own redirection settings
    (clipboard, drives, audio, microphone, printers). Defaults for a new connection (decided on
    2026-11-13): clipboard, audio and microphone on, drives and printers off.
14. **Direct Hyper-V connections.** A VM can be reached through its Hyper-V host, the way the Hyper-V
    console does it, without network access inside the VM. Decided on 2026-11-17:
    - only the Hyper-V host on this computer is needed; RemoteMe signs in to it with the current Windows
      account (which must be allowed to use Hyper-V), so a VM connection has no user name or password;
    - each VM connection chooses an enhanced session (on by default) or a basic one. An enhanced
      session needs Remote Desktop inside the VM, follows the window size and has redirection; the VM
      asks for its own password in a Windows dialog every time, and nothing is remembered. A basic
      session is the VM's console: it works without Remote Desktop inside the VM, keeps the VM's own
      resolution and has no redirection; its sign-in screen gets Ctrl+Alt+Del from "Send Ctrl+Alt+Del"
      in the connection's menu.
15. **Reconnect on connection loss.** A session reconnects automatically when the connection drops.
    Open sessions are not restored on the next start. The reconnect is the RDP component's own (it
    rides out network interruptions); when it gives up, the session area shows the reason and a
    "Reconnect" button, and RemoteMe does not retry by itself (decided on 2026-11-13). Closing the main
    window closes the open sessions without asking (decided on 2026-11-13).
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
  to be needed. For a VM the form shows the Hyper-V host (`localhost` by default), the VM ID and an
  "Enhanced session" check box instead of the user name and password; for a basic session the
  redirection settings are grayed out (decided on 2026-11-17).
- **Connection list on the left.** Groups without a root node and without tree lines (a section caption
  with a collapse arrow; while there are no groups, a flat list). The caption is a band tinted with
  the accent color of the Windows settings (the selection color in high contrast), with a bar of that
  color on the left and the number of its connections on the right (decided on 2026-11-17; the accent
  color on 2026-11-18). There is no gap between the captions and the rows: collapsed groups stand band to
  band (decided on 2026-11-18). A new accent color in the Windows settings repaints it. Two lines per connection: the name in bold (decided on 2026-11-18), and
  below it the user name and address in gray. Status dot on the line of the name (decided on 2026-11-18): green - connected, yellow - connecting or
  reconnecting (decided on 2026-11-13), empty circle - not connected, red - error. The row of the session currently on screen is highlighted
  edge to edge, with square corners and no margin around it, and a hovered row the same way in a light
  tint (decided on 2026-11-19). Actions
  (edit, disconnect, delete) are available from a three-dot button on hover and from the right-click
  menu. At the bottom, under a thin line, are flat buttons with icons and tooltips instead of captions
  (decided on 2026-11-17): "Add connection" (a plus) on the left; on the right the full-screen button
  (see the session area) and "More commands" (three dots), whose menu holds "Change master password"
  and "Log" (decided on 2026-11-13), and also "Hide list", "Clock time zone...", "Background" and "About RemoteMe". The buttons have no margins, so a hovered button is filled up to
  the edges, like the clock and the rows (decided on 2026-11-19).
- **Clock above the list buttons** (decided on 2026-11-17, option B of four mockups). Between the thin
  line and the buttons: the date, under it the time of the chosen time zone in large digits, without
  seconds, and under that the zone's main city with its UTC offset, "Yekaterinburg · UTC+5" (this order
  decided on 2026-11-19, option 3 of six mockups); the gaps between the lines are even. The full list
  gets as narrow as these lines allow, so the city and its offset stay whole; narrower than that is
  only the compact strip (decided on 2026-11-19). The city comes from the zone's IANA
  name, since Windows names a zone by several cities: "Brussels, Copenhagen, Madrid, Paris" shows
  Paris. A left click on the clock, or "Clock time zone..." in the "..." menu, opens a window with the
  Windows time zones and a search box that finds a city or a UTC offset (+7, -3:30), the offset of
  now or the standard one; the first line is the local time of Windows, the default, and it is found
  the same way. The zone is kept in the settings file; a file without it
  shows the local time.
- **Compact strip** (decided on 2026-11-19). The list dragged narrower than the clock snaps, when let
  go, to a strip 44 pixels wide or back to the clock's width, whichever is nearer. In the strip every
  connection without a group and of an open group is a circle, those of collapsed groups are not
  shown, and a thin line sets the groups apart instead of their captions. The circle has the color of
  the state (green - connected, yellow - connecting or reconnecting, red - error, gray - not
  connected) and two letters: the first two of a one-word name, or the first letters of the first two
  words; spaces, underscores and hyphens part the words (HOST - HO, Legal Data Space - LD, PRO_AGENT -
  PA). A ring marks the session on screen, and the tooltip gives the full name. Clicks, keys and the
  right-click menu work as in the list; there is no dragging. The clock shows only its time (decided on 2026-11-19) and the buttons stand
  one under another. When the circles do not fit in the height, the strip gets wider by the scroll bar.
- **Order by dragging** (decided on 2026-11-13). Connections and groups are put in order by dragging
  them in the list, and the order is kept in the settings file. Connections without a group come
  first. A connection dropped on a group caption goes first in that group; dragged above the first
  caption, it leaves its group. A group appears when a connection names it and disappears with its
  last connection. Collapsed groups are kept in the settings file and stay collapsed at the next run
  (decided on 2026-11-19).
- **Password fields** have a "Show password" check box, off by default, that shows the typed text
  (decided on 2026-11-13). A saved connection password is never loaded into the form, so it cannot be
  shown.
- **Every change is saved at once.** There is no "Save" command. If the file was changed outside the
  application since it was read (copied from another machine), saving asks whether to read the file
  again (the change just made is lost) or overwrite it (decided on 2026-11-13). The file is not
  watched.
- **No tabs.** The list replaces them: a click on a row switches to its session (a session in a
  separate window or in full screen is brought forward). A row without a running session is only
  selected: the session area shows its state ("Not connected.", "Disconnected." or the reason) with a
  "Connect" or "Reconnect" button. A double click or Enter on the row connects it (decided on
  2026-11-18).
- **About window** (decided on 2026-11-19), "About RemoteMe" at the end of the "..." menu: the icon,
  the version with the commit the exe is built from, such as "0.6.0 (57730fa)" (the commit comes from
  assembly metadata, since the product version of the exe stays the bare release version for
  SignPath), one line about the program, "Copyright © 2026 Vitalii Guliaev" and the notice the GPL asks
  an interactive program to show (no warranty, free software under the GPL version 3, with a link to
  the license), the address of the repository in full as a link, links to the releases and the issues,
  what it uses (the Remote
  Desktop component of Windows and the Segoe MDL2 Assets icon font), and the details for a problem
  report - the version, Windows, the Remote Desktop component, the .NET runtime and the settings file -
  with "Copy" and "Open settings folder" buttons.
- **Doodle background of the left panel** (decided on 2026-11-19). Below the connection rows (the rows
  themselves stay white; in the compact strip the doodles go behind the circles too), behind the
  clock and the list buttons: a soft gradient with a light pattern of small doodles (monitors, clouds,
  coffee cups, rockets, cats and the like) that the application draws itself. It comes in six colors -
  Green (the default), Blue, Peach, Lavender, Mint and Sand - or None, the plain panel as it was before the doodles, chosen
  with "Background" in the "..." menu and kept in the settings file. The session area stays plain gray. The clock stands on the
  doodles with a white glow around its text, and its date and city are nearly black (option 5 of five
  mockups).
- **No bottom notification bar.** The disconnect reason and the connection progress are shown in the
  session area (with a "Reconnect" button). Settings file errors are shown in a message box. A log of
  all messages is kept in memory and opens in a separate window on demand; it is not written to disk.
- **Session area:**
  - the resolution follows the window, with no scrollbars and no stretching;
  - the list can be hidden with one key or button, so the session takes the whole window: "Hide list"
    in the "..." menu or Ctrl+Alt+Home; a narrow strip at the left edge brings it back (decided on
    2026-11-13);
  - the main window can go full screen with the list: the full-screen button at the bottom of the list
    makes the window cover its monitor, taskbar included, without a title bar, and the list stays.
    The same button returns it to a window of the size it had, maximized or not (decided on
    2026-11-17). This is not a session's own full screen (requirement 8), which shows the session
    alone.
- **Application icon** (chosen on 2026-11-17): a white monitor with the letter R on a teal tile,
  option B of the four kept in [docs/icons](docs/icons). `src/RemoteMe/Assets/RemoteMe.ico` (16 to 256
  pixels) is drawn by `dotnet run tools/MakeIcon.cs`, run from the repository root; the exe and every
  window use it.
- **Hotkeys** (decided on 2026-11-13). They work while the focus is inside a session and do not reach
  it: Ctrl+Alt+Break - full screen and back, Ctrl+Alt+Home - hide or show the list, Ctrl+Alt+PgDn and
  Ctrl+Alt+PgUp - next and previous session in list order.

## Technical notes

- The component is the Microsoft RDP Client ActiveX control (`mstscax.dll`), the same one that mstsc uses.
- Write the wrapper by hand: our own class derived from `AxHost` plus hand-declared COM interfaces. Then
  there are no separate interop DLLs and no need to generate them with `aximp.exe` (`dotnet build`
  cannot do that). The declarations come from the type library inside `mstscax.dll` (see
  [prototypes](prototypes/README.md)). Dual interfaces are declared dispatch-only with their DISPIDs, so
  only the members in use are listed; IUnknown-only interfaces (`IMsRdpClientNonScriptable5`,
  `IMsRdpExtendedSettings`) list every vtable slot in order. The coclass is
  `MsRdpClient9NotSafeForScripting`, the oldest one with `UpdateSessionDisplaySettings`.
- Resolution on the fly: `UpdateSessionDisplaySettings` once the size or DPI stops changing. The RDP
  control calls it 300 ms after the last change of its size, DPI, parent or visibility, which covers
  dragging the border, maximizing, moving to another monitor or window, and switching sessions; the
  remote desktop follows in about 30 ms. A minimized window changes nothing. All session controls in
  the main window stay visible and docked, the active one on top, because WinForms leaves hidden
  controls out of layout: a hidden session kept its old resolution after the window was resized.
- Full screen: the component runs with `ContainerHandledFullScreen`; setting its `FullScreen` property
  makes it ask the container to go full screen, and RemoteMe moves the session control into a
  borderless window over the current monitor (rather than changing the border of the window it came
  from). From memory, not verified: the component then treats the session as full screen, so with the
  default keyboard hook mode Windows key combinations go to the session.
- Full screen with the list changes the main window itself: no border, and bounds equal to the
  monitor's. Changing the border style keeps the window handle (checked on 2026-11-17), so the session
  controls inside are not re-created. The taskbar stays below the window while it is in the foreground.
- Hotkeys: a low-level keyboard hook (the only place keys typed into a session pass through), active
  only while a RemoteMe window is in the foreground; a matched key is swallowed. Hooks run newest first,
  so the hook is installed again after each sign-in and on entering full screen, to stay ahead of the
  component's own hook.
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
- A Hyper-V VM is reached with the settings the stage 1 probe connected with: the host as the server,
  port 2179, the VM ID in `PCB` (with `;EnhancedMode=1` for an enhanced session), the service class
  `Microsoft Virtual Console Service`, CredSSP on, `NegotiateSecurityLayer` off,
  `DisableCredentialsDelegation` on, and an empty user name and password. Credential prompts are
  allowed only for an enhanced session, for the VM's own sign-in. A basic session keeps the VM's
  resolution, so the window size is not sent to it. "Send Ctrl+Alt+Del" uses the component's
  `SendKeys` with scan codes.

## Build

Requires the .NET 10 SDK on Windows (`global.json` pins 10.0.404 and allows newer feature bands).

```
dotnet build RemoteMe.slnx
dotnet test RemoteMe.slnx
dotnet format RemoteMe.slnx --verify-no-changes
dotnet publish src/RemoteMe/RemoteMe.csproj -p:PublishProfile=SingleFile
```

The last command produces one self-contained `src/RemoteMe/bin/publish/RemoteMe.exe` (win-x64).

GitHub Actions (decided on 2026-11-17):

- `.github/workflows/build.yml` builds, tests and checks the formatting on every push and pull request
  to `main`.
- `.github/workflows/release.yml` runs when a tag such as `v0.1.0` is pushed. It runs the tests,
  publishes the exe with that version and publishes a GitHub release at once with `RemoteMe.exe` and
  `RemoteMe.exe.sha256`. Only tags of the form `vX.Y.Z` are accepted.

To make a release:

```
git tag v0.1.0
git push origin v0.1.0
```

To check a download, compare the output of `Get-FileHash RemoteMe.exe` (PowerShell) with the hash in
`RemoteMe.exe.sha256` and in the release notes.

## Code signing policy

Releases are not signed (decided on 2026-11-17). SignPath Foundation, which signs open source projects
for free, declined the application on 2026-11-17: it accepts projects that are already widely known.
The project can apply again later. An unsigned exe makes Windows SmartScreen warn about an unknown
publisher; check each download against its SHA-256 as described under "Build".

The release workflow keeps a signing step through SignPath for that case. It runs only when the
repository variable `SIGNPATH_ORGANIZATION_ID` is set, and then also needs the variables
`SIGNPATH_PROJECT_SLUG` and `SIGNPATH_SIGNING_POLICY_SLUG` and the secret `SIGNPATH_API_TOKEN`. It waits
up to an hour for the signing request to be approved by hand in SignPath, checks the signature and
releases the signed exe. The notes of each release say whether its exe is signed. The artifact
configuration in SignPath (the workflow uploads `RemoteMe.exe` as the only file of a GitHub artifact
and passes the version from the tag):

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
  <parameters>
    <parameter name="version" required="true" />
  </parameters>
  <zip-file>
    <pe-file path="RemoteMe.exe" product-name="RemoteMe" product-version="${version}">
      <authenticode-sign/>
    </pe-file>
  </zip-file>
</artifact-configuration>
```

Releases are built from the tagged source by GitHub Actions (`.github/workflows/release.yml`) on
GitHub-hosted runners.

Privacy policy: This program will not transfer any information to other networked systems unless
specifically requested by the user or the person installing or operating it.

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
      compression. Rechecked on 2026-11-17 with the finished application: a connection attempt loaded
      `mstscax.dll` from `System32`, and nothing appeared next to the exe except the settings file or in
      `%TEMP%\.net`.
- [ ] Which key combinations reach the application while the focus is inside the remote desktop.
      So far: the WinForms message loop sees none of them (see the technical notes), a low-level hook
      does. With the default keyboard hook mode (2, full screen only) Alt+Tab and Ctrl+Esc act on the
      local computer in a window. Stage 5 builds the hotkeys on that hook (see the technical notes);
      whether they work inside a session, in a window and in full screen, is checked by hand in stage 5.
      Modes 0 and 1 were not tried: the default mode stays.
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
      in. Settled on 2026-11-17 (see requirement 14): only the local host, the guest password stays
      in the Windows dialog, and the basic session gets "Send Ctrl+Alt+Del". Stage 6 builds this into
      the application; still to check by hand there: an enhanced and a basic session, and whether
      "Send Ctrl+Alt+Del" reaches the guest (the probe sent it, the result was not confirmed).
- [x] Server certificates: whether the component exposes the certificate thumbprint, so the consent can
      be stored in the settings file and travel between machines. Fallback: the component's own "do not
      ask again" option, which Windows stores in the registry on each machine separately.
      Checked on 2026-11-13: it does not. The type library has no certificate property, and
      `OnReceivedTSPublicKey` (with `NotifyTSPublicKey` on) was not raised in six connections with NLA;
      from memory, it belongs to the old RDP security layer. The fallback applies (decided on
      2026-11-17, requirement 12). Consent given in mstsc is shared with the component.
- [x] Automatic reconnect: what the component does by itself and what we have to handle.
      Checked on 2026-11-13: with `EnableAutoReconnect` the component reconnects by itself after a
      network loss (`OnAutoReconnecting2`, then `OnAutoReconnected` and `OnLoginComplete`; 19 seconds in
      the test). While it reconnects, the component draws its own "reconnecting" message over the
      session. It does not reconnect after the guest restarts (`OnDisconnected` 0x3, extended reason 26).
      We handle the disconnects it gives up on and the texts.
- [x] Size of the final exe (CdRescue on the same platform is about 50 MB).
      Stage 0 skeleton: 46.9 MB with `EnableCompressionInSingleFile`, 102.1 MB without. Warm start to
      the first window is about 200 ms compressed and about 135 ms uncompressed. Compression is on.
      Finished application (2026-11-17): 47.0 MB; the master password window appeared about 0.4 s
      after start, measured by polling UI Automation, which adds its own delay.

## Open questions

- Whether one session stretched across several monitors is needed (a separate RDP feature, full screen
  only).
## Findings along the way

- **PIN does not work in a Hyper-V enhanced session** by design: this mode runs over RDP. In the VM,
  turn off Settings - Accounts - Sign-in options - "only allow Windows Hello sign-in for Microsoft
  accounts" and sign in with a password.
- **mstsc** changes the resolution only when the window is maximized or switched to full screen, not when
  the window border is dragged (from memory, not verified).
