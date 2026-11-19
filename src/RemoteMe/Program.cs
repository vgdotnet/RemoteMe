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
        SettingsStore? store;
        using (var unlock = UnlockForm.ForStartup(settingsPath)) {
            if (unlock.ShowDialog() != DialogResult.OK || unlock.Store is null) {
                return;
            }
            store = unlock.Store;
        }

        using (store) {
            using var main = new MainForm(store, log, logPath);
            using var watchdog = new HangWatchdog(main, log);
            main.Load += (_, _) => watchdog.Start();
            Application.Run(main);
        }
    }
}
