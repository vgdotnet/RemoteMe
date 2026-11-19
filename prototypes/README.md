# Prototypes

Throwaway tools that checked how the RDP component behaves before RemoteMe was written. They are not
part of `RemoteMe.slnx` and are never published. Build and run each project directly.

## TypeLibDump

Dumps a COM type library: coclasses, interfaces with IIDs, DISPIDs, vtable slots and parameter types,
enums. The RDP interface declarations in `RdpProbe/Interop` were taken from this dump of the component
installed on Windows, not from memory.

```
dotnet run --project prototypes/TypeLibDump -- C:\Windows\System32\mstscax.dll <output.txt>
```

The dump describes a Microsoft binary and is not committed.

## RdpProbe

A WinForms window that hosts the RDP control (`MsRdpClient9NotSafeForScripting`) through a hand-written
`AxHost` and hand-declared COM interfaces, with buttons for each experiment and an event log.

```
dotnet run --project prototypes/RdpProbe
```

- The server, user name and password are typed into the window and kept in memory only. The log does
  not contain the password, the server or the user name.
- Key logging covers only combinations with Ctrl, Alt or Win and service keys (F1-F24, Esc and
  similar). Plain characters are never logged; for Ctrl+Alt combinations the character is masked,
  since Ctrl+Alt is AltGr on some layouts. The low-level hook logs only while a probe window is in
  the foreground.
- Settings apply at the next "Connect".

Experiments:

| Item to verify | How |
|---|---|
| Moving the control between windows | "Move: WinForms Parent" and "Move: Win32 SetParent" while connected |
| Keys inside the session | Press combinations with the focus in the session; try each keyboard hook mode and the two swallow options |
| Scale on the fly | Move the window to a monitor with a different scale; the log shows each `UpdateSessionDisplaySettings` call |
| Server certificates | "Report the server public key", the authentication level and "Reject the public key" |
| Automatic reconnect | Disconnect the VM network adapter for a while with "Automatic reconnect by the component" on |
| Direct Hyper-V connection | "VM on a Hyper-V host" mode: port 2179, VM ID in `PCB`, optional enhanced session |
