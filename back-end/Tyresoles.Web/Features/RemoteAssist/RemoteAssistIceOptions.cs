namespace Tyresoles.Web.Features.RemoteAssist;

/// <summary>STUN/TURN URLs exposed to clients (configure in appsettings RemoteAssist:IceServers).</summary>
public sealed class RemoteAssistIceOptions
{
    public List<RemoteAssistIceServer> IceServers { get; set; } = new()
    {
        new RemoteAssistIceServer { Urls = "stun:stun.l.google.com:19302" }
    };
}

public sealed class RemoteAssistIceServer
{
    public string Urls { get; set; } = "";
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
