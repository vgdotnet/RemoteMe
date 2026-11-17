namespace RemoteMe.Core.Sessions;

public sealed record DisconnectInfo(string Message, bool IsError);

// Our own texts for the disconnect reasons: the component's GetErrorDescription is unreliable (it says
// "An internal error has occurred" for a disconnect the user asked for). Codes marked "seen" were
// observed in the stage 1 prototype; the rest come from the component's documentation as remembered.
public static class DisconnectReasons {
    private static readonly Dictionary<int, DisconnectInfo> Extended = new() {
        [1] = new("The session was disconnected from the remote computer.", false),
        [2] = new("The session was signed out from the remote computer.", false),
        [3] = new("The session was disconnected because it was idle too long.", false),
        [4] = new("Signing in to the remote computer took too long.", true),
        [5] = new("Another connection took over this session.", false),
        [6] = new("The remote computer ran out of memory.", true),
        [7] = new("The remote computer refused the connection.", true), // seen
        [9] = new("This account is not allowed to connect to the remote computer.", true),
        [10] = new("The remote computer wants the password to be entered again.", true),
        [11] = new("The session was disconnected from the remote computer.", false),
        [12] = new("You signed out of the remote session.", false),
        [25] = new("The remote computer is shutting down.", false),
        [26] = new("The remote computer is restarting.", false), // seen
    };

    private static readonly Dictionary<int, DisconnectInfo> Reasons = new() {
        [0x1] = new("Disconnected.", false), // seen
        [0x2] = new("The session was ended on the remote computer.", false),
        [0x3] = new("The remote computer ended the session.", false), // seen
        [0x104] = new("The computer name could not be found.", true),
        [0x107] = new("The connection was cancelled at the certificate warning.", false), // seen
        [0x108] = new("The remote computer did not answer in time.", true),
        [0x204] = new("The remote computer could not be reached. Check that it is on and accepts remote connections.", true), // seen
        [0x807] = new("The user name or password is not accepted.", true), // seen
        [0x904] = new("The connection was lost.", true),
        [0xF08] = new("Windows does not allow sending the credentials to this computer.", true), // seen
    };

    public static DisconnectInfo Describe(int reason, int extendedReason, string componentText) {
        if (extendedReason is >= 256 and <= 267) {
            return new DisconnectInfo("The remote computer has a licensing problem.", true);
        }
        if (Extended.TryGetValue(extendedReason, out var extended)) {
            return extended;
        }
        if (Reasons.TryGetValue(reason, out var known)) {
            return known;
        }
        var text = string.IsNullOrWhiteSpace(componentText) ? "The connection ended." : componentText.Trim();
        return new DisconnectInfo($"{text} (code 0x{reason:X})", true);
    }
}
