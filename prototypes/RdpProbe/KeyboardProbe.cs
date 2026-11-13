using System.Runtime.InteropServices;
using static RdpProbe.NativeMethods;

namespace RdpProbe;

// Logs key combinations seen by the application in two places: the WinForms message loop and a
// low-level keyboard hook. Plain character keys are never logged, so text typed into the session
// (passwords included) does not reach the log.
internal sealed class KeyboardProbe : IMessageFilter, IDisposable {
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly Action<string> _log;
    private readonly Func<IntPtr, bool> _isInsideSession;
    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hook;

    public KeyboardProbe(Action<string> log, Func<IntPtr, bool> isInsideSession) {
        _log = log;
        _isInsideSession = isInsideSession;
        _hookProc = HookCallback;
    }

    public bool SwallowInMessageLoop { get; set; }

    public bool SwallowInHook { get; set; }

    public void InstallHook() {
        RemoveHook();
        _hook = SetWindowsHookEx(WhKeyboardLl, _hookProc, GetModuleHandle(null), 0);
        _log(_hook == IntPtr.Zero
            ? $"keys: hook install failed, error {Marshal.GetLastWin32Error()}"
            : "keys: low-level hook installed (now first in the hook chain)");
    }

    public void RemoveHook() {
        if (_hook == IntPtr.Zero) {
            return;
        }
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _log("keys: low-level hook removed");
    }

    public bool PreFilterMessage(ref Message m) {
        if (m.Msg != WmKeyDown && m.Msg != WmSysKeyDown) {
            return false;
        }
        var combo = Describe((Keys)(int)m.WParam);
        if (combo is null) {
            return false;
        }
        var inSession = _isInsideSession(m.HWnd);
        var swallow = SwallowInMessageLoop && inSession;
        _log($"keys: message loop [{(inSession ? "session" : "app")}] {combo}{(swallow ? " - swallowed" : "")}");
        return swallow;
    }

    public void Dispose() {
        RemoveHook();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
        if (nCode >= 0 && (wParam == WmKeyDown || wParam == WmSysKeyDown) && IsOwnWindowInForeground()) {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var combo = Describe((Keys)data.VkCode);
            if (combo is not null) {
                _log($"keys: low-level hook {combo}{(SwallowInHook ? " - swallowed" : "")}");
                if (SwallowInHook) {
                    return 1;
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool IsOwnWindowInForeground() {
        GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        return processId == Environment.ProcessId;
    }

    private static string? Describe(Keys key) {
        key &= Keys.KeyCode;
        if (IsModifier(key)) {
            return null;
        }
        var ctrl = IsDown(VkControl);
        var alt = IsDown(VkMenu);
        var win = IsDown(VkLWin) || IsDown(VkRWin);
        var shift = IsDown(VkShift);
        var character = IsCharacter(key);
        if (!ctrl && !alt && !win && !IsServiceKey(key)) {
            return null;
        }
        // Ctrl+Alt is AltGr on some layouts, so the character itself is not shown.
        var keyName = ctrl && alt && character ? "<character>" : key.ToString();
        var parts = new List<string>();
        if (ctrl) {
            parts.Add("Ctrl");
        }
        if (alt) {
            parts.Add("Alt");
        }
        if (shift) {
            parts.Add("Shift");
        }
        if (win) {
            parts.Add("Win");
        }
        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsModifier(Keys key) => key is Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
        or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
        or Keys.Menu or Keys.LMenu or Keys.RMenu
        or Keys.LWin or Keys.RWin;

    private static bool IsServiceKey(Keys key) => key is >= Keys.F1 and <= Keys.F24
        or Keys.Escape or Keys.Apps or Keys.PrintScreen or Keys.Pause or Keys.Scroll;

    private static bool IsCharacter(Keys key) => key is >= Keys.A and <= Keys.Z
        or >= Keys.D0 and <= Keys.D9
        or >= Keys.NumPad0 and <= Keys.Divide
        or Keys.Space
        or >= Keys.OemSemicolon and <= Keys.Oemtilde
        or >= Keys.OemOpenBrackets and <= Keys.Oem8
        or Keys.OemBackslash;
}
