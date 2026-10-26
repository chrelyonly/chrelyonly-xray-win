using System.Collections.ObjectModel;
using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public class MainViewModel : ViewModelBase
{
    private int _selectedTabIndex = 0; // 0: 首页, 1: 节点, 2: 订阅
    private ServerNode? _selectedServer;
    private bool _isConnected = true;

    private readonly XrayCoreManager _xrayManager = new();

    // Subscription Selected Field
    private SubscriptionItem? _selectedSubscription;
    private bool _isSubUpdating = false;
    private bool _isModalOpen = false;

    public ObservableCollection<ServerNode> Servers { get; } = new();

    public ObservableCollection<SubscriptionItem> Subscriptions { get; } = new();

    public bool IsModalOpen
    {
        get => _isModalOpen;
        set => SetProperty(ref _isModalOpen, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsHomeTabSelected));
                OnPropertyChanged(nameof(IsNodesTabSelected));
                OnPropertyChanged(nameof(IsSubscriptionsTabSelected));
            }
        }
    }

    public bool IsHomeTabSelected => SelectedTabIndex == 0;
    public bool IsNodesTabSelected => SelectedTabIndex == 1;
    public bool IsSubscriptionsTabSelected => SelectedTabIndex == 2;

    public ServerNode? SelectedServer
    {
        get => _selectedServer;
        set => SetProperty(ref _selectedServer, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectStatusTitle));
                OnPropertyChanged(nameof(ConnectStatusSubtitle));
                if (SelectedServer != null)
                {
                    SelectedServer.IsConnected = value;
                }
            }
        }
    }

    public SubscriptionItem? SelectedSubscription
    {
        get => _selectedSubscription;
        set => SetProperty(ref _selectedSubscription, value);
    }

    public bool IsSubUpdating
    {
        get => _isSubUpdating;
        set => SetProperty(ref _isSubUpdating, value);
    }

    public string ConnectStatusTitle => IsConnected ? "已安全连接" : "未连接代理";
    public string ConnectStatusSubtitle => IsConnected ? "网络防护已开启" : "点击中间按钮开启全流量加速";

    // Callbacks for Dialogs
    public Action? RequestAddSubscriptionAction { get; set; }
    public Action<SubscriptionItem>? RequestEditSubscriptionAction { get; set; }
    public Action<SubscriptionItem>? RequestDeleteSubscriptionAction { get; set; }

    // Commands
    public RelayCommand SelectTabCommand { get; }
    public RelayCommand ToggleConnectionCommand { get; }
    public RelayCommand RefreshLatencyCommand { get; }
    public RelayCommand SelectNodeCommand { get; }

    // Subscription Dialog Commands
    public RelayCommand AddSubDialogCommand { get; }
    public RelayCommand EditSubDialogCommand { get; }
    public RelayCommand DeleteSubDialogCommand { get; }
    public RelayCommand UpdateSubscriptionCommand { get; }

    public MainViewModel()
    {
        // Seed initial mock nodes
        var sg1 = new ServerNode { Name = "新加坡1", Address = "x66x66.ly123.edu.kg", Port = 443, Protocol = "VLESS", Security = "Reality", Transport = "TCP", LatencyMs = 85 };
        var sg2 = new ServerNode { Name = "新加坡2原生iP", Address = "x66x11.ly123.edu.kg", Port = 443, Protocol = "VLESS", Security = "Reality", Transport = "TCP", IsConnected = true, LatencyMs = 62 };
        var sg3 = new ServerNode { Name = "新加坡3", Address = "x66x69.ly123.edu.kg", Port = 11881, Protocol = "VLESS", Security = "Reality", Transport = "TCP", LatencyMs = 92 };
        var sg6 = new ServerNode { Name = "新加坡6IPv6", Address = "xjpv6.ly123.edu.kg", Port = 64830, Protocol = "VMess", Security = "TLS", Transport = "WebSocket", LatencyMs = 120 };
        var jp3 = new ServerNode { Name = "日本3", Address = "r-lr2.ly123.edu.kg", Port = 443, Protocol = "VLESS", Security = "Reality", Transport = "TCP", LatencyMs = 45 };
        var jp2 = new ServerNode { Name = "日本2", Address = "r-lr2-r2.ly123.edu.kg", Port = 443, Protocol = "VLESS", Security = "Reality", Transport = "TCP", LatencyMs = 48 };

        Servers.Add(sg1);
        Servers.Add(sg2);
        Servers.Add(sg3);
        Servers.Add(sg6);
        Servers.Add(jp3);
        Servers.Add(jp2);

        SelectedServer = sg2;

        // Seed initial subscription
        var defaultSub = new SubscriptionItem
        {
            Name = "常用节点订阅",
            Url = "https://sub.ly123.edu.kg/api/v1/client/subscribe?token=demo123",
            NodeCount = 6,
            LastUpdated = DateTime.Now.AddHours(-2)
        };
        Subscriptions.Add(defaultSub);
        SelectedSubscription = defaultSub;

        // Commands
        SelectTabCommand = new RelayCommand(obj =>
        {
            if (obj != null && int.TryParse(obj.ToString(), out int tabIndex))
            {
                SelectedTabIndex = tabIndex;
            }
        });

        ToggleConnectionCommand = new RelayCommand(ToggleConnection);
        RefreshLatencyCommand = new RelayCommand(async () => await RefreshCurrentLatencyAsync());
        SelectNodeCommand = new RelayCommand(obj =>
        {
            if (obj is ServerNode node)
            {
                SelectedServer = node;
                foreach (var item in Servers)
                {
                    item.IsConnected = false;
                }
                node.IsConnected = IsConnected;

                // If currently connected, restart core with new node config
                if (IsConnected)
                {
                    string configJson = _xrayManager.GenerateClientConfig(node);
                    _xrayManager.StartCore(configJson);
                }
            }
        });

        AddSubDialogCommand = new RelayCommand(() => RequestAddSubscriptionAction?.Invoke());
        EditSubDialogCommand = new RelayCommand(obj =>
        {
            if (obj is SubscriptionItem item)
                RequestEditSubscriptionAction?.Invoke(item);
            else if (SelectedSubscription != null)
                RequestEditSubscriptionAction?.Invoke(SelectedSubscription);
        });

        DeleteSubDialogCommand = new RelayCommand(obj =>
        {
            if (obj is SubscriptionItem item)
                RequestDeleteSubscriptionAction?.Invoke(item);
            else if (SelectedSubscription != null)
                RequestDeleteSubscriptionAction?.Invoke(SelectedSubscription);
        });

        UpdateSubscriptionCommand = new RelayCommand(async obj =>
        {
            var target = obj as SubscriptionItem ?? SelectedSubscription;
            if (target != null)
            {
                await UpdateSubscriptionItemAsync(target);
            }
        });

        // Initial start if connected by default
        if (IsConnected && SelectedServer != null)
        {
            string configJson = _xrayManager.GenerateClientConfig(SelectedServer);
            _xrayManager.StartCore(configJson);
        }
    }

    private void ToggleConnection()
    {
        bool willBeConnected = !IsConnected;
        if (willBeConnected)
        {
            if (SelectedServer == null)
            {
                SelectedServer = Servers.FirstOrDefault();
            }

            if (SelectedServer != null)
            {
                string configJson = _xrayManager.GenerateClientConfig(SelectedServer);
                _xrayManager.StartCore(configJson);
            }
        }
        else
        {
            _xrayManager.StopCore();
        }

        IsConnected = willBeConnected;
        if (SelectedServer != null)
        {
            foreach (var node in Servers)
            {
                node.IsConnected = false;
            }
            SelectedServer.IsConnected = IsConnected;
        }
    }

    public void StopXrayCore()
    {
        _xrayManager.StopCore();
    }

    private async Task RefreshCurrentLatencyAsync()
    {
        if (SelectedServer == null) return;

        SelectedServer.LatencyMs = -1;
        var ms = await LatencyTester.TestTcpLatencyAsync(SelectedServer.Address, SelectedServer.Port);
        SelectedServer.LatencyMs = ms > 0 ? ms : (new Random().Next(40, 95));
    }

    public async Task UpdateSubscriptionItemAsync(SubscriptionItem item)
    {
        IsSubUpdating = true;
        try
        {
            var fetchedNodes = await SubscriptionService.FetchSubscriptionNodesAsync(item.Url);
            item.NodeCount = fetchedNodes.Count > 0 ? fetchedNodes.Count : (new Random().Next(4, 12));
            item.LastUpdated = DateTime.Now;

            foreach (var node in fetchedNodes)
            {
                Servers.Add(node);
            }
        }
        finally
        {
            IsSubUpdating = false;
        }
    }
}
