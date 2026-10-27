using chrelyonly_xray_win.ViewModels;

namespace chrelyonly_xray_win.Models;

public class ServerNode : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString();
    private string _subscriptionId = "";
    private string _name = "新节点";
    private string _address = "127.0.0.1";
    private int _port = 443;
    private string _email = "";
    private string _host = "";
    private string _ech = "";
    private string _path = "";
    private string _encryption = "";
    private string _obfs = "";
    private string _obfs_password = "";
    private string _protocol = "VLESS"; // VLESS, VMess, Trojan, Shadowsocks
    private string _security = "Reality"; // Reality, TLS, None
    private string _transport = "TCP"; // TCP, WebSocket, gRPC
    private string _uuid = "";
    private string _sni = "";
    private string _publicKey = "";
    private string _shortId = "";
    private string _spx = "";
    private string _flow = "";
    private bool _isConnected;
    private int _latencyMs = -1; // -1: untested, 0: timeout
    private bool _isTestingLatency = false;
    private bool _isOpenAiUnlocked = true;
    private bool _isClaudeUnlocked = true;
    private bool _isGeminiUnlocked = true;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string SubscriptionId
    {
        get => _subscriptionId;
        set => SetProperty(ref _subscriptionId, value);
    }

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

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }


    public string ObfsPassword
    {
        get => _obfs_password;
        set => SetProperty(ref _obfs_password, value);
    }
    public string Obfs
    {
        get => _obfs;
        set => SetProperty(ref _obfs, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }
    public string Ech
    {
        get => _ech;
        set => SetProperty(ref _ech, value);
    }
    public string Encryption
    {
        get => _encryption;
        set => SetProperty(ref _encryption, value);
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

    public string Sni
    {
        get => _sni;
        set => SetProperty(ref _sni, value);
    }

    public string PublicKey
    {
        get => _publicKey;
        set => SetProperty(ref _publicKey, value);
    }

    public string ShortId
    {
        get => _shortId;
        set => SetProperty(ref _shortId, value);
    }
    public string Spx
    {
        get => _spx;
        set => SetProperty(ref _spx, value);
    }

    public string Flow
    {
        get => _flow;
        set => SetProperty(ref _flow, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionStatusText));
            }
        }
    }

    public int LatencyMs
    {
        get => _latencyMs;
        set
        {
            if (SetProperty(ref _latencyMs, value))
            {
                OnPropertyChanged(nameof(LatencyDisplay));
                OnPropertyChanged(nameof(LatencyColor));
            }
        }
    }

    public bool IsTestingLatency
    {
        get => _isTestingLatency;
        set
        {
            if (SetProperty(ref _isTestingLatency, value))
            {
                OnPropertyChanged(nameof(LatencyDisplay));
            }
        }
    }

    public string LatencyDisplay
    {
        get
        {
            if (_isTestingLatency) return "测速中...";
            if (_latencyMs < 0) return "-- ms";
            if (_latencyMs == 0 || _latencyMs > 9999) return "超时";
            return $"{_latencyMs} ms";
        }
    }

    public string LatencyColor
    {
        get
        {
            if (_isTestingLatency) return "#F59E0B"; // 橙色
            if (_latencyMs < 0) return "#9CA3AF";     // 灰色
            if (_latencyMs == 0 || _latencyMs > 9999) return "#EF4444"; // 红色
            if (_latencyMs < 150) return "#10B981";  // 绿色
            if (_latencyMs < 300) return "#F59E0B";  // 黄色
            return "#EF4444";                        // 红色
        }
    }

    public bool IsOpenAiUnlocked
    {
        get => _isOpenAiUnlocked;
        set => SetProperty(ref _isOpenAiUnlocked, value);
    }

    public bool IsClaudeUnlocked
    {
        get => _isClaudeUnlocked;
        set => SetProperty(ref _isClaudeUnlocked, value);
    }

    public bool IsGeminiUnlocked
    {
        get => _isGeminiUnlocked;
        set => SetProperty(ref _isGeminiUnlocked, value);
    }

    public string ConnectionStatusText => IsConnected ? "• 已连接" : "未连接";
}
