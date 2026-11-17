using System.Runtime.InteropServices;
using static RemoteMe.NativeMethods;

namespace RemoteMe.Input;

internal enum Hotkey {
    FullScreen,
    ToggleList,
    NextSession,
    PreviousSession,
}

// Ctrl+Alt hotkeys through a low-level keyboard hook: keys typed into a session never pass through the
// WinForms message loop (the RDP input window runs on its own thread). It acts only while a RemoteMe
// window is in the foreground, and a matched key is swallowed so the session does not get it.
internal sealed class HotkeyHook : IDisposable {
    private const int VkCancel = 0x03;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkPause = 0x13;
    private const int VkPrior = 0x21;
    private const int VkNext = 0x22;
    private const int VkHome = 0x24;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly Action<Hotkey> _onHotkey;
    private readonly SynchronizationContext _context;
    private readonly LowLevelKeyboardProc _callback;
    private readonly HashSet<uint> _heldKeys = [];
    private IntPtr _hook;

    public HotkeyHook(Action<Hotkey> onHotkey) {
        _onHotkey = onHotkey;
        _context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("The hotkey hook must be created on the UI thread.");
        _callback = Callback;
        Install();
    }

    // Hooks are called newest first; reinstalling puts this one ahead of the RDP component's own hook.
    public void Reinstall() {
        Uninstall();
        Install();
    }

    public void Dispose() {
        Uninstall();
    }

    private void Install() {
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
    }

    private void Uninstall() {
        if (_hook != IntPtr.Zero) {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam) {
        if (nCode >= 0) {
            var key = Marshal.PtrToStructure<KbdLlHookStruct>(lParam).VkCode;
            var message = (int)wParam;
            if (message is WmKeyUp or WmSysKeyUp && _heldKeys.Remove(key)) {
                return 1;
            }
            if (message is WmKeyDown or WmSysKeyDown) {
                var match = Match(key);
                if (_heldKeys.Contains(key)) {
                    if (match is not null) {
                        return 1;
                    }
                    // The release was missed (another window had the keyboard): forget the key.
                    _heldKeys.Remove(key);
                }
                if (match is { } hotkey && IsOwnWindowInForeground()) {
                    _heldKeys.Add(key);
                    _context.Post(_ => _onHotkey(hotkey), null);
                    return 1;
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static Hotkey? Match(uint key) {
        if (!IsDown(VkControl) || !IsDown(VkMenu) || IsDown(VkShift) || IsDown(VkLWin) || IsDown(VkRWin)) {
            return null;
        }
        return key switch {
            VkCancel or VkPause => Hotkey.FullScreen,
            VkHome => Hotkey.ToggleList,
            VkNext => Hotkey.NextSession,
            VkPrior => Hotkey.PreviousSession,
            _ => null,
        };
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsOwnWindowInForeground() {
        GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        return processId == Environment.ProcessId;
    }
}
