using RemoteMe.App;
using RemoteMe.Core.Logging;
using RemoteMe.Core.Vault;
using RemoteMe.UI;

namespace RemoteMe;

internal static class Program {
    private const string SettingsFileName = "RemoteMe.settings";

    [STAThread]
    private static void Main() {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null) {
            SingleInstance.ActivateExisting();
            return;
        }

        ApplicationConfiguration.Initialize();
        var log = new AppLog();
        ErrorHandling.Install(log);

        var settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        SettingsStore? store;
        using (var unlock = UnlockForm.ForStartup(settingsPath)) {
            if (unlock.ShowDialog() != DialogResult.OK || unlock.Store is null) {
                return;
            }
            store = unlock.Store;
        }

        using (store) {
            Application.Run(new MainForm(store, log));
        }
    }
}
