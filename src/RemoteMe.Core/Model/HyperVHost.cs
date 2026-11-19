using System.Net;
using System.Net.NetworkInformation;

namespace RemoteMe.Core.Model;

// A Hyper-V host is signed in to with the current Windows account, so only this computer is accepted as
// one (requirement 14): any other name would get that account's sign-in.
public static class HyperVHost {
    public const string Requirement = "The Hyper-V host must be this computer: localhost, its name or one of its addresses.";

    public static bool IsThisComputer(string host) {
        var name = host.Trim();
        if (name.Equals("localhost", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (IPAddress.TryParse(name, out var address)) {
            return IPAddress.IsLoopback(address) || LocalAddresses().Contains(address);
        }
        return ComputerNames().Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ComputerNames() {
        yield return Environment.MachineName;
        yield return Dns.GetHostName();
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        if (properties.DomainName.Length > 0) {
            yield return $"{properties.HostName}.{properties.DomainName}";
        }
    }

    private static IEnumerable<IPAddress> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces().SelectMany(i => i.GetIPProperties().UnicastAddresses).Select(a => a.Address);
}
