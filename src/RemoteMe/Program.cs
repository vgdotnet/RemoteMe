using RemoteMe.App;
using RemoteMe.Core.Logging;
using RemoteMe.Core.Vault;
using RemoteMe.UI;

namespace RemoteMe;

internal static class Program {
    private const string SettingsFileName = "RemoteMe.settings";
    private const string LogSource = "Application";

    [STAThread]
    private static void Main() {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null) {
            SingleInstance.ActivateExisting();
            return;
        }

        ApplicationConfiguration.Initialize();
        // The log goes to the Logs folder next to the exe, one file per computer, since the folder may be
        // shared by several computers through a cloud drive.
        var logs = Path.Combine(AppContext.BaseDirectory, "Logs");
        using var logFile = new LogFile(Path.Combine(logs, Environment.MachineName + ".log"));
        var log = new AppLog();
        log.Added += (_, entry) => logFile.Write(entry);
        ErrorHandling.Install(log, logFile.Flush);
        var marker = new RunMarker(Path.Combine(logs, Environment.MachineName + ".running"));

        var started = DateTimeOffset.Now;
        log.Information(LogSource, $"RemoteMe {AboutForm.Version} started. {AboutForm.Platform}");
        if (marker.Begin(started) is { } previous) {
            log.Warning(LogSource, $"The previous run, started at {previous:yyyy-MM-dd HH:mm:ss}, did not close normally.");
            CrashReports.CopyToLog(log, previous, Path.GetFileName(Environment.ProcessPath) ?? "RemoteMe.exe");
        }
        try {
            Run(log, logFile.Path);
        } finally {
            log.Information(LogSource, "RemoteMe closed.");
            marker.End();
        }
    }

    private static void Run(AppLog log, string logPath) {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        // The master password saved on this computer sits next to the settings, one file per computer.
        var remembered = RememberedPassword.ForThisComputer(settingsPath);
        var store = OpenRemembered(settingsPath, remembered, log, out var notice);
        if (store is null) {
            using var unlock = UnlockForm.ForStartup(settingsPath, remembered, notice);
            if (unlock.ShowDialog() != DialogResult.OK || unlock.Store is null) {
                return;
            }
            store = unlock.Store;
            ApplyRememberChoice(store, unlock.Remember, log);
        }

        using (store) {
            using var main = new MainForm(store, log, logPath);
            using var watchdog = new HangWatchdog(main, log);
            main.Load += (_, _) => watchdog.Start();
            Application.Run(main);
        }
    }

    // No store means the unlock window is needed; the notice then says why the saved password did not do.
    private static SettingsStore? OpenRemembered(string settingsPath, RememberedPassword remembered, AppLog log, out string? notice) {
        notice = null;
        try {
            var store = SettingsStore.OpenRemembered(settingsPath, remembered, out var state);
            switch (state) {
                case RememberedPasswordState.Fits:
                    log.Information(LogSource, "Opened with the master password saved on this computer.");
                    break;
                case RememberedPasswordState.PasswordChanged:
                    log.Warning(LogSource, "The master password saved on this computer does not fit: the master password was changed since then.");
                    notice = "The master password was changed after it was saved on this computer. Enter the current one.";
                    break;
                case RememberedPasswordState.Unreadable:
                    log.Warning(LogSource, "The master password saved on this computer could not be read.");
                    notice = "The master password saved on this computer could not be read. Enter it again.";
                    break;
            }
            return store;
        } catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException) {
            log.Error(LogSource, $"The settings file could not be opened with the master password saved on this computer: {ex.Message}");
            notice = $"The settings file could not be opened with the master password saved on this computer. {ex.Message}";
            return null;
        }
    }

    private static void ApplyRememberChoice(SettingsStore store, bool remember, AppLog log) {
        try {
            if (remember) {
                store.Remember();
                log.Information(LogSource, "The master password was saved on this computer.");
            } else if (store.IsRemembered) {
                store.Forget();
                log.Information(LogSource, "The master password saved on this computer was removed.");
            }
        } catch (RememberedPasswordException ex) {
            log.Error(LogSource, ex.Message);
            Messages.Error(null, ex.Message);
        }
    }
}
