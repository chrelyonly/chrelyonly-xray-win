using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public class AddServerViewModel : ViewModelBase
{
    private string _name = "新节点";
    private string _address = "";
    private int _port = 443;
    private string _protocol = "VLESS";
    private string _security = "Reality";
    private string _transport = "TCP";
    private string _uuid = "";
    private string _shareLink = "";

    public string[] Protocols { get; } = ["VLESS", "VMess", "Trojan", "Shadowsocks"];
    public string[] Securities { get; } = ["Reality", "TLS", "None"];
    public string[] Transports { get; } = ["TCP", "WebSocket", "gRPC"];

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Protocol
    {
        get => _protocol;
        set => SetProperty(ref _protocol, value);
    }

    public string Security
    {
        get => _security;
        set => SetProperty(ref _security, value);
    }

    public string Transport
    {
        get => _transport;
        set => SetProperty(ref _transport, value);
    }

    public string Uuid
    {
        get => _uuid;
        set => SetProperty(ref _uuid, value);
    }

    public string ShareLink
    {
        get => _shareLink;
        set
        {
            if (SetProperty(ref _shareLink, value))
            {
                TryImportShareLink(value);
            }
        }
    }

    public RelayCommand ParseLinkCommand { get; }

    public AddServerViewModel()
    {
        ParseLinkCommand = new RelayCommand(() => TryImportShareLink(ShareLink));
    }

    public AddServerViewModel(ServerNode existingNode) : this()
    {
        Name = existingNode.Name;
        Address = existingNode.Address;
        Port = existingNode.Port;
        Protocol = existingNode.Protocol;
        Security = existingNode.Security;
        Transport = existingNode.Transport;
        Uuid = existingNode.Uuid;
    }

    private void TryImportShareLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link)) return;
        var parsed = ProxyLinkParser.ParseShareLink(link);
        if (parsed != null)
        {
            Name = parsed.Name;
            Address = parsed.Address;
            Port = parsed.Port;
            Protocol = parsed.Protocol;
            Security = parsed.Security;
            Transport = parsed.Transport;
            Uuid = parsed.Uuid;
        }
    }

    public ServerNode ToServerNode()
    {
        return new ServerNode
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "未命名节点" : Name,
            Address = Address,
            Port = Port,
            Protocol = Protocol,
            Security = Security,
            Transport = Transport,
            Uuid = Uuid
        };
    }
}
