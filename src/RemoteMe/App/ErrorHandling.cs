using RemoteMe.Core.Logging;
using RemoteMe.UI;

namespace RemoteMe.App;

// Every exception that reaches the top goes to the log with its stack trace. One on the window's thread
// is shown and RemoteMe goes on; one on another thread ends the process, so the log file is written
// before the message.
internal static class ErrorHandling {
    private const string Source = "Application";

    public static void Install(AppLog log, Action flush) {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => {
            log.Error(Source, $"Unhandled exception: {e.Exception}");
            Messages.Error(null, $"Something went wrong: {e.Exception.Message}");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => {
            log.Error(Source, $"Unhandled exception, RemoteMe closes: {e.ExceptionObject}");
            flush();
            if (e.ExceptionObject is Exception exception) {
                Messages.Error(null, $"Something went wrong: {exception.Message}");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) => log.Error(Source, $"Unobserved task exception: {e.Exception}");
    }
}
