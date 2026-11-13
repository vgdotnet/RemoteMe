using System.ComponentModel;

namespace RdpProbe.Interop;

[DesignerCategory("Code")]
internal sealed class RdpClientHost : AxHost {
    private readonly IMsTscAxEvents _sink;
    private ConnectionPointCookie? _cookie;
    private IMsRdpClient9? _client;

    public RdpClientHost(IMsTscAxEvents sink) : base(RdpClassIds.MsRdpClient9NotSafeForScripting) {
        _sink = sink;
    }

    internal IMsRdpClient9 Client => _client ?? throw new InvalidOperationException("The RDP control is not created yet.");

    internal IMsRdpClientNonScriptable5 NonScriptable => (IMsRdpClientNonScriptable5)Ocx;

    internal IMsRdpExtendedSettings ExtendedSettings => (IMsRdpExtendedSettings)Ocx;

    internal object? CurrentOcx => GetOcx();

    private object Ocx => GetOcx() ?? throw new InvalidOperationException("The RDP control is not created yet.");

    protected override void AttachInterfaces() {
        _client = (IMsRdpClient9)Ocx;
    }

    protected override void CreateSink() {
        _cookie = new ConnectionPointCookie(Ocx, _sink, typeof(IMsTscAxEvents));
    }

    protected override void DetachSink() {
        _cookie?.Disconnect();
        _cookie = null;
    }
}
