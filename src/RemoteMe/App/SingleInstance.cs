using System.Diagnostics;
using static RemoteMe.NativeMethods;

namespace RemoteMe.App;

internal sealed class SingleInstance : IDisposable {
    // Global namespace: one instance per machine, across user sessions.
    private const string MutexName = @"Global\RemoteMe.SingleInstance";

    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex) {
        _mutex = mutex;
    }

    public static SingleInstance? TryAcquire() {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew) {
            return new SingleInstance(mutex);
        }
        mutex.Dispose();
        return null;
    }

    public static void ActivateExisting() {
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(current.ProcessName)) {
            using (process) {
                var window = process.MainWindowHandle;
                if (process.Id == current.Id || window == IntPtr.Zero) {
                    continue;
                }
                ShowWindow(window, IsIconic(window) ? SwRestore : SwShow);
                SetForegroundWindow(window);
                return;
            }
        }
    }

    public void Dispose() {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
