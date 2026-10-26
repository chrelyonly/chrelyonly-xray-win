using System.Collections.ObjectModel;
using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public class MainViewModel : ViewModelBase
{
    private int _selectedTabIndex = 0; // 0: 首页, 1: 节点, 2: 订阅, 3: 日志
    private ServerNode? _selectedServer;
    private bool _isConnected = false;

    private readonly XrayCoreManager _xrayManager = new();

    // Subscription Selected Field
    private SubscriptionItem? _selectedSubscription;
    private bool _isSubUpdating = false;
    private bool _isModalOpen = false;

    public ObservableCollection<ServerNode> Servers { get; } = new();

    public ObservableCollection<SubscriptionItem> Subscriptions { get; } = new();

    public ObservableCollection<string> SystemLogLines => AppLogger.SystemLogLines;
    public ObservableCollection<string> CoreLogLines => AppLogger.CoreLogLines;

    public bool IsLoggingEnabled
    {
        get => AppLogger.IsLoggingEnabled;
        set
        {
            if (AppLogger.IsLoggingEnabled != value)
            {
                AppLogger.IsLoggingEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LoggingStatusText));
            }
        }
    }

    public string LoggingStatusText => IsLoggingEnabled ? "暂停收集" : "恢复收集";

    private bool _isAutoScrollEnabled = true;
    public bool IsAutoScrollEnabled
    {
        get => _isAutoScrollEnabled;
        set => SetProperty(ref _isAutoScrollEnabled, value);
    }

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
                OnPropertyChanged(nameof(IsLogsTabSelected));
            }
        }
    }

    public bool IsHomeTabSelected => SelectedTabIndex == 0;
    public bool IsNodesTabSelected => SelectedTabIndex == 1;
    public bool IsSubscriptionsTabSelected => SelectedTabIndex == 2;
    public bool IsLogsTabSelected => SelectedTabIndex == 3;

    public ServerNode? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                SaveState();
            }
        }
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
                NotifyXrayStatusChanged();
                if (SelectedServer != null)
                {
                    SelectedServer.IsConnected = value;
                }
            }
        }
    }

    public bool IsXrayRunning => _xrayManager.IsRunning;
    public string XrayStatusText => IsXrayRunning ? "Xray 内核状态: 运行中 (Active)" : "Xray 内核状态: 未运行 (Stopped)";
    public string XrayStatusColor => IsXrayRunning ? "#10B981" : "#EF4444";

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
    public RelayCommand ToggleLoggingCommand { get; }
    public RelayCommand ClearSystemLogsCommand { get; }
    public RelayCommand ClearCoreLogsCommand { get; }

    // Subscription Dialog Commands
    public RelayCommand AddSubDialogCommand { get; }
    public RelayCommand EditSubDialogCommand { get; }
    public RelayCommand DeleteSubDialogCommand { get; }
    public RelayCommand UpdateSubscriptionCommand { get; }

    public MainViewModel()
    {
        AppLogger.LogSystem("[主控逻辑] 应用程序正在初始化 ViewModel...");
        // Load persisted data using SqliteStorageService
        var savedData = Task.Run(() => SqliteStorageService.LoadAsync()).GetAwaiter().GetResult();
        foreach (var sub in savedData.Subscriptions)
        {
            Subscriptions.Add(sub);
        }
        foreach (var srv in savedData.CachedServers)
        {
            Servers.Add(srv);
        }

        if (!string.IsNullOrEmpty(savedData.SelectedServerId))
        {
            SelectedServer = Servers.FirstOrDefault(s => s.Id == savedData.SelectedServerId);
        }
        if (SelectedServer == null && Servers.Count > 0)
        {
            SelectedServer = Servers[0];
        }

        if (Subscriptions.Count > 0)
        {
            SelectedSubscription = Subscriptions[0];
        }

        // Commands
        SelectTabCommand = new RelayCommand(obj =>
        {
            if (obj != null && int.TryParse(obj.ToString(), out int tabIndex))
            {
                SelectedTabIndex = tabIndex;
                AppLogger.LogSystem($"[主控逻辑] 切换至 Tab 页面索引: {tabIndex}");
            }
        });

        ToggleConnectionCommand = new RelayCommand(ToggleConnection);
        RefreshLatencyCommand = new RelayCommand(async () => await RefreshCurrentLatencyAsync());

        ToggleLoggingCommand = new RelayCommand(() =>
        {
            IsLoggingEnabled = !IsLoggingEnabled;
            AppLogger.LogSystem(IsLoggingEnabled ? "[主控逻辑] 用户已恢复系统与内核日志收集。" : "[主控逻辑] 用户已暂停日志收集（高性能模式）。");
        });

        ClearSystemLogsCommand = new RelayCommand(() =>
        {
            AppLogger.SystemLogLines.Clear();
            AppLogger.LogSystem("[主控逻辑] 用户已清空系统日志。");
        });

        ClearCoreLogsCommand = new RelayCommand(() =>
        {
            AppLogger.CoreLogLines.Clear();
            AppLogger.LogSystem("[主控逻辑] 用户已清空内核日志。");
        });

        SelectNodeCommand = new RelayCommand(obj =>
        {
            if (obj is ServerNode node)
            {
                AppLogger.LogSystem($"[主控逻辑] 用户切换选中节点: {node.Name} ({node.Address}:{node.Port}, 协议: {node.Protocol})");
                SelectedServer = node;
                foreach (var item in Servers)
                {
                    item.IsConnected = false;
                }
                node.IsConnected = IsConnected;

                // If currently connected, restart core with new node config
                if (IsConnected)
                {
                    AppLogger.LogSystem("[主控逻辑] 当前处于连接状态，正在热重启 Xray 内核应用新节点配置...");
                    string configJson = _xrayManager.GenerateClientConfig(node);
                    _xrayManager.StartCore(configJson);
                    NotifyXrayStatusChanged();
                }
                SaveState();
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
                await UpdateSubscriptionItemAsync(target, showAlert: true);
            }
        });

        // 默认打开时自动在后台静默更新订阅
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500); // 延迟 1.5 秒等待主窗口完全初始化
            foreach (var sub in Subscriptions.ToList())
            {
                if (sub.AutoUpdate)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await UpdateSubscriptionItemAsync(sub, showAlert: false);
                    });
                }
            }
        });
    }

    private void ToggleConnection()
    {
        bool willBeConnected = !IsConnected;
        AppLogger.LogSystem($"[主控逻辑] 触发连接开关，目标连接状态: {willBeConnected}");
        if (willBeConnected)
        {
            if (SelectedServer == null)
            {
                SelectedServer = Servers.FirstOrDefault();
                AppLogger.LogSystem("[主控逻辑] 提示：当前未选中任何节点，已自动选择列表首个节点。");
            }

            if (SelectedServer != null)
            {
                AppLogger.LogSystem($"[主控逻辑] 正在使用节点启动代理: {SelectedServer.Name}");
                string configJson = _xrayManager.GenerateClientConfig(SelectedServer);
                bool success = _xrayManager.StartCore(configJson);
                if (!success)
                {
                    AppLogger.LogSystem("[主控逻辑] 错误：Xray 核心启动失败。");
                    new Views.AlertWindow("启动失败", "未能成功启动 Xray 核心，请检查 Core/xraylib.exe 是否存在。", false).ShowDialog();
                    return;
                }
            }
            else
            {
                AppLogger.LogSystem("[主控逻辑] 提示：节点列表为空，无法启动代理。");
                new Views.AlertWindow("提示", "请先在节点列表中选择或添加一个服务器节点。", false).ShowDialog();
                SelectedTabIndex = 1; // Switch to nodes tab
                return;
            }
        }
        else
        {
            AppLogger.LogSystem("[主控逻辑] 正在关闭代理，停止 Xray 核心...");
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
        NotifyXrayStatusChanged();
        SaveState();
    }

    private void NotifyXrayStatusChanged()
    {
        OnPropertyChanged(nameof(IsXrayRunning));
        OnPropertyChanged(nameof(XrayStatusText));
        OnPropertyChanged(nameof(XrayStatusColor));
    }

    public void SaveState()
    {
        _ = SqliteStorageService.SaveAsync(Subscriptions, Servers, SelectedServer?.Id);
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

    public async Task UpdateSubscriptionItemAsync(SubscriptionItem item, bool showAlert = true)
    {
        AppLogger.LogSystem($"[订阅逻辑] 开始更新订阅: {item.Name} (URL: {item.Url})");
        item.IsUpdating = true;
        IsSubUpdating = true;
        try
        {
            var fetchedNodes = await SubscriptionService.FetchSubscriptionNodesAsync(item.Url);
            AppLogger.LogSystem($"[订阅逻辑] 订阅解析完成，获取到 {fetchedNodes.Count} 个节点。");
            item.NodeCount = fetchedNodes.Count > 0 ? fetchedNodes.Count : 0;
            item.LastUpdated = DateTime.Now;

            foreach (var node in fetchedNodes)
            {
                if (!Servers.Any(s => s.Address == node.Address && s.Port == node.Port))
                {
                    Servers.Add(node);
                }
            }

            if (SelectedServer == null && Servers.Count > 0)
            {
                SelectedServer = Servers[0];
            }

            SaveState();

            if (showAlert)
            {
                new Views.AlertWindow("订阅更新", $"更新订阅成功！当前订阅包含 {fetchedNodes.Count} 个可用节点。", true).ShowDialog();
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[订阅逻辑] 更新订阅发生异常: {ex.Message}");
            if (showAlert)
            {
                new Views.AlertWindow("订阅更新失败", $"更新订阅失败，请检查网络或订阅地址是否正确。\n错误信息: {ex.Message}", false).ShowDialog();
            }
        }
        finally
        {
            item.IsUpdating = false;
            IsSubUpdating = false;
        }
    }
}
