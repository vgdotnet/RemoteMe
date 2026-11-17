using RemoteMe.Core.Logging;
using RemoteMe.UI;

namespace RemoteMe.App;

internal static class ErrorHandling {
    public static void Install(AppLog log) {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Show(log, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => {
            if (e.ExceptionObject is Exception exception) {
                Show(log, exception);
            }
        };
    }

    private static void Show(AppLog log, Exception exception) {
        log.Error("Application", $"{exception.GetType().Name}: {exception.Message}");
        Messages.Error(null, $"Something went wrong: {exception.Message}");
    }
}
