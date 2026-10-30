using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
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
    private bool _isSubUpdating = false;
    private bool _isModalOpen = false;
    private bool _isTestingAiUnlock = false;
    private bool _isTestingAllNodes = false;

    private bool _allowLan = false;
    private bool _isSystemProxyEnabled = false;
    private bool _isGlobalMode = false;
    private string _customProxyRules = "";
    private string _customDirectRules = "";
    private bool _isAutoStartEnabled = false;
    private string _memoryUsageText = "0.0 MB";
    private string _uploadSpeedText = "0.0 KB/s";
    private string _downloadSpeedText = "0.0 KB/s";
    private long _lastBytesReceived = 0;
    private long _lastBytesSent = 0;
    private DateTime _lastTrafficCheckTime = DateTime.MinValue;

    // Traffic Stats Accumulation
    private long _totalSessionUploadedBytes = 0;
    private long _totalSessionDownloadedBytes = 0;
    private string _totalUploadText = "0 B";
    private string _totalDownloadText = "0 B";

    // Speed Test Fields
    private bool _isSpeedTesting = false;
    private double _speedTestProgress = 0;
    private string _speedTestProgressText = "0.0 / 50.0 MB (0%)";
    private string _speedTestCurrentSpeedText = "0 B/s";
    private string _speedTestPeakSpeedText = "0 B/s";
    private string _speedTestAverageSpeedText = "0 B/s";
    private string _speedTestStatusText = "就绪 (准备节点测速)";
    private CancellationTokenSource? _speedTestCts;

    // IP Prompt Fields
    private string _localLanIpText = SystemProxyManager.GetLocalLanIp();
    private string _localPublicIpText = "点击刷新查询";
    private string _outboundIpText = "点击刷新查询";
    private string _outboundLocationText = "";
    private bool _isCheckingIp = false;

    public string UploadSpeedText
    {
        get => _uploadSpeedText;
        set => SetProperty(ref _uploadSpeedText, value);
    }

    public string DownloadSpeedText
    {
        get => _downloadSpeedText;
        set => SetProperty(ref _downloadSpeedText, value);
    }

    public string TotalUploadText
    {
        get => _totalUploadText;
        set => SetProperty(ref _totalUploadText, value);
    }

    public string TotalDownloadText
    {
        get => _totalDownloadText;
        set => SetProperty(ref _totalDownloadText, value);
    }

    public bool IsSpeedTesting
    {
        get => _isSpeedTesting;
        set => SetProperty(ref _isSpeedTesting, value);
    }

    public double SpeedTestProgress
    {
        get => _speedTestProgress;
        set => SetProperty(ref _speedTestProgress, value);
    }

    public string SpeedTestProgressText
    {
        get => _speedTestProgressText;
        set => SetProperty(ref _speedTestProgressText, value);
    }

    public string SpeedTestCurrentSpeedText
    {
        get => _speedTestCurrentSpeedText;
        set => SetProperty(ref _speedTestCurrentSpeedText, value);
    }

    public string SpeedTestPeakSpeedText
    {
        get => _speedTestPeakSpeedText;
        set => SetProperty(ref _speedTestPeakSpeedText, value);
    }

    public string SpeedTestAverageSpeedText
    {
        get => _speedTestAverageSpeedText;
        set => SetProperty(ref _speedTestAverageSpeedText, value);
    }

    public string SpeedTestStatusText
    {
        get => _speedTestStatusText;
        set => SetProperty(ref _speedTestStatusText, value);
    }

    public string LocalLanIpText
    {
        get => _localLanIpText;
        set => SetProperty(ref _localLanIpText, value);
    }

    public string LocalPublicIpText
    {
        get => _localPublicIpText;
        set => SetProperty(ref _localPublicIpText, value);
    }

    public string OutboundIpText
    {
        get => _outboundIpText;
        set => SetProperty(ref _outboundIpText, value);
    }

    public string OutboundLocationText
    {
        get => _outboundLocationText;
        set => SetProperty(ref _outboundLocationText, value);
    }

    public bool IsCheckingIp
    {
        get => _isCheckingIp;
        set => SetProperty(ref _isCheckingIp, value);
    }

    public ObservableCollection<ServerNode> Servers { get; } = new();

    public ObservableCollection<SubscriptionItem> Subscriptions { get; } = new();

    public ObservableCollection<AiUnlockItem> AiUnlockItems { get; } = new();

    public ObservableCollection<string> SystemLogLines => AppLogger.SystemLogLines;
    public ObservableCollection<string> CoreLogLines => AppLogger.CoreLogLines;

    public string LanIpAddress => SystemProxyManager.GetLocalLanIp();

    public string GenerateCurrentConfig(ServerNode? node = null)
    {
        var srv = node ?? SelectedServer;
        if (srv == null) return "";
        return _xrayManager.GenerateClientConfig(srv, AllowLan, IsGlobalMode, CustomProxyRules, CustomDirectRules);
    }

    private void ReapplyXrayConfigIfConnected(ServerNode? targetNode = null)
    {
        if (IsConnected)
        {
            var srv = targetNode ?? SelectedServer;
            if (srv != null)
            {
                _ = Task.Run(() =>
                {
                    string configJson = GenerateCurrentConfig(srv);
                    _xrayManager.StartCore(configJson);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        NotifyXrayStatusChanged();
                    });
                });
            }
        }
    }

    public bool AllowLan
    {
        get => _allowLan;
        set
        {
            if (SetProperty(ref _allowLan, value))
            {
                OnPropertyChanged(nameof(AllowLanStatusText));
                SaveState();

                if (IsConnected)
                {
                    AppLogger.LogSystem($"[局域网共享] 局域网共享已切换为: {value}，正在重新应用 Xray 监听配置...");
                    ReapplyXrayConfigIfConnected();
                }
            }
        }
    }

    public string AllowLanStatusText => AllowLan ? "局域网共享已开启 (监听 0.0.0.0)" : "仅限本机访问 (监听 127.0.0.1)";

    public bool IsGlobalMode
    {
        get => _isGlobalMode;
        set
        {
            if (SetProperty(ref _isGlobalMode, value))
            {
                OnPropertyChanged(nameof(RoutingModeStatusText));
                SaveState();

                if (IsConnected)
                {
                    AppLogger.LogSystem($"[路由模式] 路由模式已变更为: {(value ? "全局代理" : "智能分流")}，正在重启 Xray 应用配置...");
                    ReapplyXrayConfigIfConnected();
                }
            }
        }
    }

    public string CustomProxyRules
    {
        get => _customProxyRules;
        set
        {
            if (SetProperty(ref _customProxyRules, value))
            {
                SaveState();
                ReapplyXrayConfigIfConnected();
            }
        }
    }

    public string CustomDirectRules
    {
        get => _customDirectRules;
        set
        {
            if (SetProperty(ref _customDirectRules, value))
            {
                SaveState();
                ReapplyXrayConfigIfConnected();
            }
        }
    }

    public bool IsAutoStartEnabled
    {
        get => _isAutoStartEnabled;
        set
        {
            if (SetProperty(ref _isAutoStartEnabled, value))
            {
                SaveState();
                SystemProxyManager.SetAutoStart(value);
            }
        }
    }

    public string RoutingModeStatusText => IsGlobalMode ? "全局代理模式 (所有流量走代理)" : "智能分流模式 (大陆直连/海外代理)";

    public string AppVersionText => "v20261030修仙版;Xray: 26.10.21";

    public string MemoryUsageText
    {
        get => _memoryUsageText;
        set => SetProperty(ref _memoryUsageText, value);
    }

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

    public bool IsTestingAiUnlock
    {
        get => _isTestingAiUnlock;
        set => SetProperty(ref _isTestingAiUnlock, value);
    }

    public bool IsTestingAllNodes
    {
        get => _isTestingAllNodes;
        set => SetProperty(ref _isTestingAllNodes, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsHomeTabSelected));
                OnPropertyChanged(nameof(IsSpeedStatsTabSelected));
                OnPropertyChanged(nameof(IsNodesTabSelected));
                OnPropertyChanged(nameof(IsSubscriptionsTabSelected));
                OnPropertyChanged(nameof(IsLogsTabSelected));
                OnPropertyChanged(nameof(IsSettingsTabSelected));
            }
        }
    }

    public bool IsHomeTabSelected => SelectedTabIndex == 0;
    public bool IsSpeedStatsTabSelected => SelectedTabIndex == 1;
    public bool IsNodesTabSelected => SelectedTabIndex == 2;
    public bool IsSubscriptionsTabSelected => SelectedTabIndex == 3;
    public bool IsLogsTabSelected => SelectedTabIndex == 4;
    public bool IsSettingsTabSelected => SelectedTabIndex == 5;

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
                if (SelectedServer != null)
                {
                    SelectedServer.IsConnected = value;
                }
            }
        }
    }

    public bool IsXrayRunning => _xrayManager.IsRunning;
    public string XrayStatusText => IsXrayRunning ? "Xray 内核状态: 运行中" : "Xray 内核状态: 未运行";
    public string XrayStatusColor => IsXrayRunning ? "#10B981" : "#EF4444";

    public bool IsSystemProxyEnabled
    {
        get => _isSystemProxyEnabled;
        set
        {
            if (value)
            {
                var (isProxyActive, proxyServer, isOwn) = SystemProxyManager.CheckSystemProxyConflict(10909);
                if (isProxyActive && !isOwn)
                {
                    AppLogger.LogSystem($"[系统代理冲突] 检测到当前系统代理已被第三方程序占用: {proxyServer}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        new Views.AlertWindow("代理冲突提示", $"检测到当前 Windows 系统代理已被其他程序占用 ({proxyServer})。", true).ShowDialog();
                    });
                }
            }

            if (SetProperty(ref _isSystemProxyEnabled, value))
            {
                OnPropertyChanged(nameof(SystemProxyStatusText));
                OnPropertyChanged(nameof(SystemProxyStatusColor));
                SystemProxyManager.SetSystemProxy(value, "127.0.0.1:10909");
                SaveState();
            }
        }
    }

    public string SystemProxyStatusText => IsSystemProxyEnabled ? "系统代理状态: 已开启" : "系统代理状态: 已关闭";
    public string SystemProxyStatusColor => IsSystemProxyEnabled ? "#10B981" : "#6B7280";

    private bool _isUseProxyForSubUpdate = false;
    public bool IsUseProxyForSubUpdate
    {
        get => _isUseProxyForSubUpdate;
        set
        {
            if (SetProperty(ref _isUseProxyForSubUpdate, value))
            {
                SaveState();
                AppLogger.LogSystem($"[订阅配置] 使用代理更新订阅开关已更改为: {value}");
            }
        }
    }

    private bool _isNotificationsEnabled = true;
    public bool IsNotificationsEnabled
    {
        get => _isNotificationsEnabled;
        set
        {
            if (SetProperty(ref _isNotificationsEnabled, value))
            {
                SaveState();
                AppLogger.LogSystem($"[系统设置] Windows消息通知开关已更改为: {value}");
            }
        }
    }

    private bool _isInitializing = true;
    private SubscriptionItem? _selectedSubscription;

    public SubscriptionItem? SelectedSubscription
    {
        get => _selectedSubscription;
        set
        {
            if (_isInitializing)
            {
                AppLogger.LogSystem("[订阅逻辑] 提示：应用程序正在启动初始化中，暂时不允许切换订阅。");
                return;
            }
            if (SetProperty(ref _selectedSubscription, value))
            {
                if (value != null)
                {
                    Servers.Clear();
                    foreach (var node in value.Nodes)
                    {
                        Servers.Add(node);
                    }
                    AppLogger.LogSystem($"[订阅逻辑] 切换订阅源 -> {value.Name}，已加载 {value.Nodes.Count} 个节点。");

                    if (SelectedServer == null || !Servers.Contains(SelectedServer))
                    {
                        SelectedServer = Servers.FirstOrDefault();
                    }
                }
                SaveState();
            }
        }
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

    // AI & Latency Testing Commands
    public RelayCommand TestAiUnlockItemCommand { get; }
    public RelayCommand TestAllAiUnlockCommand { get; }
    public RelayCommand TestNodeLatencyCommand { get; }
    public RelayCommand TestAllNodesLatencyCommand { get; }
    public RelayCommand SortNodesByLatencyCommand { get; }

    // Subscription Dialog Commands
    public RelayCommand AddSubDialogCommand { get; }
    public RelayCommand EditSubDialogCommand { get; }
    public RelayCommand DeleteSubDialogCommand { get; }
    public RelayCommand UpdateSubscriptionCommand { get; }

    // Traffic, Speed Test & IP Commands
    public RelayCommand ResetTrafficCommand { get; }
    public RelayCommand StartSpeedTestCommand { get; }
    public RelayCommand CancelSpeedTestCommand { get; }
    public RelayCommand RefreshIpInfoCommand { get; }

    // Routing Rules Commands
    public RelayCommand SaveRulesCommand { get; }
    public RelayCommand ResetRulesCommand { get; }

    public MainViewModel()
    {
        AppLogger.LogSystem("[主控逻辑] 应用程序正在初始化 ViewModel...");

        // Initialize AI Platforms
        AiUnlockItems.Add(new AiUnlockItem { Key = "Baidu", Name = "Baidu", TestUrl = "https://www.baidu.com" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "Google", Name = "Google", TestUrl = "https://www.google.com" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "YouTube", Name = "YouTube", TestUrl = "https://www.youtube.com" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "OpenAI", Name = "OpenAI", TestUrl = "https://chatgpt.com" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "Claude", Name = "Claude", TestUrl = "https://claude.ai" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "Gemini", Name = "Gemini", TestUrl = "https://gemini.google.com" });

        // Load persisted data using SqliteStorageService
        var savedData = Task.Run(() => SqliteStorageService.LoadAsync()).GetAwaiter().GetResult();
        foreach (var sub in savedData.Subscriptions)
        {
            Subscriptions.Add(sub);
        }

        if (Subscriptions.Count > 0)
        {
            _selectedSubscription = Subscriptions[0];
            Servers.Clear();
            foreach (var node in _selectedSubscription.Nodes)
            {
                Servers.Add(node);
            }
        }

        if (Servers.Count == 0 && savedData.CachedServers.Count > 0)
        {
            foreach (var srv in savedData.CachedServers)
            {
                if (Subscriptions.Count > 0)
                {
                    Subscriptions[0].Nodes.Add(srv);
                }
                Servers.Add(srv);
            }
        }

        if (!string.IsNullOrEmpty(savedData.SelectedServerId))
        {
            SelectedServer = Servers.FirstOrDefault(s => s.Id == savedData.SelectedServerId);
        }

        // Restore Settings from persistence
        if (savedData.Settings.TryGetValue("AllowLan", out string? allowLanStr) && bool.TryParse(allowLanStr, out bool savedAllowLan))
        {
            _allowLan = savedAllowLan;
        }
        if (savedData.Settings.TryGetValue("IsSystemProxyEnabled", out string? sysProxyStr) && bool.TryParse(sysProxyStr, out bool savedSysProxy))
        {
            _isSystemProxyEnabled = savedSysProxy;
        }
        else
        {
            _isSystemProxyEnabled = SystemProxyManager.GetSystemProxyStatus();
        }
        if (savedData.Settings.TryGetValue("IsGlobalMode", out string? globalModeStr) && bool.TryParse(globalModeStr, out bool savedGlobalMode))
        {
            _isGlobalMode = savedGlobalMode;
        }
        if (savedData.Settings.TryGetValue("IsUseProxyForSubUpdate", out string? useProxyStr) && bool.TryParse(useProxyStr, out bool savedUseProxy))
        {
            _isUseProxyForSubUpdate = savedUseProxy;
        }
        if (savedData.Settings.TryGetValue("IsNotificationsEnabled", out string? notifStr) && bool.TryParse(notifStr, out bool savedNotif))
        {
            _isNotificationsEnabled = savedNotif;
        }
        if (savedData.Settings.TryGetValue("CustomProxyRules", out string? proxyRulesStr))
        {
            _customProxyRules = proxyRulesStr;
        }
        if (savedData.Settings.TryGetValue("CustomDirectRules", out string? directRulesStr))
        {
            _customDirectRules = directRulesStr;
        }
        _isAutoStartEnabled = SystemProxyManager.GetAutoStartStatus();

        // Start Traffic & Memory Usage Timer (1 second interval)
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        int tickCounter = 0;
        timer.Tick += (s, e) =>
        {
            UpdateTrafficSpeed();
            tickCounter++;
            if (tickCounter >= 2)
            {
                tickCounter = 0;
                UpdateMemoryUsage();
            }
        };
        timer.Start();
        UpdateTrafficSpeed();
        UpdateMemoryUsage();

        // Commands
        SelectTabCommand = new RelayCommand(obj =>
        {
            if (obj != null && int.TryParse(obj.ToString(), out int tabIndex))
            {
                SelectedTabIndex = tabIndex;
                AppLogger.LogSystem($"[主控逻辑] 切换至 Tab 页面索引: {tabIndex}");
            }
        });

        SaveRulesCommand = new RelayCommand(obj =>
        {
            SaveState();
            ReapplyXrayConfigIfConnected();
            AppLogger.LogSystem("[分流规则] 域名与 IP 分流黑白名单规则已保存并应用成功！");
            new Views.AlertWindow("规则已保存", "自定义分流规则已保存成功，并已即时生效！", true).ShowDialog();
        });

        ResetRulesCommand = new RelayCommand(obj =>
        {
            _customProxyRules = "";
            _customDirectRules = "";
            OnPropertyChanged(nameof(CustomProxyRules));
            OnPropertyChanged(nameof(CustomDirectRules));
            SaveState();
            ReapplyXrayConfigIfConnected();
            AppLogger.LogSystem("[分流规则] 已清空并重置自定义分流规则。");
            new Views.AlertWindow("规则重置", "已成功重置所有自定义分流规则。", true).ShowDialog();
        });

        ToggleConnectionCommand = new RelayCommand(ToggleConnection);
        RefreshLatencyCommand = new RelayCommand(async () => await RefreshCurrentLatencyAsync());

        TestAiUnlockItemCommand = new RelayCommand(async obj =>
        {
            if (obj is AiUnlockItem item)
            {
                await TestSingleAiUnlockAsync(item);
            }
            else if (obj is string key)
            {
                var target = AiUnlockItems.FirstOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    await TestSingleAiUnlockAsync(target);
                }
            }
        });

        TestAllAiUnlockCommand = new RelayCommand(async () => await TestAllAiUnlockAsync());

        TestNodeLatencyCommand = new RelayCommand(async obj =>
        {
            if (obj is ServerNode node)
            {
                await TestSingleNodeLatencyAsync(node);
            }
            else
            {
                await RefreshCurrentLatencyAsync();
            }
        });

        TestAllNodesLatencyCommand = new RelayCommand(async () => await TestAllNodesLatencyAsync());
        SortNodesByLatencyCommand = new RelayCommand(SortNodesByLatency);

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
                    ReapplyXrayConfigIfConnected(node);
                    _ = RefreshCurrentLatencyAsync();
                    _ = TestAllAiUnlockAsync();
                }
                else
                {
                    // 未连接时也主动测节点延迟
                    _ = TestSingleNodeLatencyAsync(node);
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

        // 流量、测速与 IP 相关 Command 绑定
        ResetTrafficCommand = new RelayCommand(() =>
        {
            _totalSessionUploadedBytes = 0;
            _totalSessionDownloadedBytes = 0;
            TotalUploadText = "0 B";
            TotalDownloadText = "0 B";
            AppLogger.LogSystem("[流量统计] 已重置累计上下传流量统计。");
        });

        StartSpeedTestCommand = new RelayCommand(async () => await StartSpeedTestAsync());
        CancelSpeedTestCommand = new RelayCommand(CancelSpeedTest);
        RefreshIpInfoCommand = new RelayCommand(async () => await RefreshIpInfoAsync());

        // 启动时初始化测速与 IP 查询（不自动更新订阅，仅支持手动更新）
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500); // 延迟 1.5 秒等待主窗口完全初始化
            _isInitializing = false; // 初始化完成，解除订阅切换限制
            AppLogger.LogSystem("[主控逻辑] 应用程序初始化完成，已解除订阅切换限制。");
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await RefreshCurrentLatencyAsync();
                await RefreshIpInfoAsync();
                if (IsConnected)
                {
                    await TestAllAiUnlockAsync();
                }
            });
        });
    }

    private void UpdateMemoryUsage()
    {
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            double workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);
            MemoryUsageText = $"{workingSetMb:F1} MB";
        }
        catch { }
    }

    private void UpdateTrafficSpeed()
    {
        try
        {
            long totalBytesReceived = 0;
            long totalBytesSent = 0;

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    try
                    {
                        var stats = ni.GetIPStatistics();
                        totalBytesReceived += stats.BytesReceived;
                        totalBytesSent += stats.BytesSent;
                    }
                    catch
                    {
                        try
                        {
                            var v4Stats = ni.GetIPv4Statistics();
                            totalBytesReceived += v4Stats.BytesReceived;
                            totalBytesSent += v4Stats.BytesSent;
                        }
                        catch { }
                    }
                }
            }

            var now = DateTime.Now;
            if (_lastTrafficCheckTime != DateTime.MinValue)
            {
                double seconds = (now - _lastTrafficCheckTime).TotalSeconds;
                if (seconds > 0)
                {
                    long diffReceived = totalBytesReceived - _lastBytesReceived;
                    long diffSent = totalBytesSent - _lastBytesSent;

                    if (diffReceived < 0) diffReceived = 0;
                    if (diffSent < 0) diffSent = 0;

                    // 累计总上下传流量
                    _totalSessionDownloadedBytes += diffReceived;
                    _totalSessionUploadedBytes += diffSent;

                    double downloadSpeed = diffReceived / seconds;
                    double uploadSpeed = diffSent / seconds;

                    DownloadSpeedText = FormatSpeed(downloadSpeed);
                    UploadSpeedText = FormatSpeed(uploadSpeed);

                    TotalDownloadText = FormatBytes(_totalSessionDownloadedBytes);
                    TotalUploadText = FormatBytes(_totalSessionUploadedBytes);
                }
            }

            _lastBytesReceived = totalBytesReceived;
            _lastBytesSent = totalBytesSent;
            _lastTrafficCheckTime = now;
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[流量统计] 更新网速失败: {ex.Message}");
        }
    }

    private string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024 * 1024):F2} TB";
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    private string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024 * 1024 * 1024)
        {
            return $"{bytesPerSecond / (1024 * 1024 * 1024):F2} GB/s";
        }
        else if (bytesPerSecond >= 1024 * 1024)
        {
            return $"{bytesPerSecond / (1024 * 1024):F2} MB/s";
        }
        else if (bytesPerSecond >= 1024)
        {
            return $"{bytesPerSecond / 1024:F1} KB/s";
        }
        else
        {
            return $"{bytesPerSecond:F0} B/s";
        }
    }

    private void ToggleConnection()
    {
        bool willBeConnected = !IsConnected;
        AppLogger.LogSystem($"[主控逻辑] 触发连接开关，目标连接状态: {willBeConnected}");
        if (willBeConnected)
        {
            if (SelectedServer == null)
            {
                AppLogger.LogSystem("[主控逻辑] 提示：当前未选中任何节点。");
                new Views.AlertWindow("提示", "请先在节点列表中选择一个服务器节点。", false).ShowDialog();
                SelectedTabIndex = 2; // Switch to nodes tab
                return;
            }

            if (SelectedServer != null)
            {
                AppLogger.LogSystem($"[主控逻辑] 正在使用节点启动代理: {SelectedServer.Name} (允许局域网: {AllowLan}, 全局模式: {IsGlobalMode})");
                var srv = SelectedServer;

                IsConnected = true;
                IsSystemProxyEnabled = true;
                foreach (var node in Servers)
                {
                    node.IsConnected = false;
                }
                srv.IsConnected = true;
                NotifyXrayStatusChanged();
                SaveState();

                _ = Task.Run(() =>
                {
                    string configJson = GenerateCurrentConfig(srv);
                    bool success = _xrayManager.StartCore(configJson);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!success)
                        {
                            AppLogger.LogSystem("[主控逻辑] 错误：Xray 核心启动失败。");
                            new Views.AlertWindow("启动失败", "未能成功启动 Xray 核心，请检查 Core/xraylib.exe 是否存在。", false).ShowDialog();
                            IsConnected = false;
                            IsSystemProxyEnabled = false;
                            srv.IsConnected = false;
                            NotifyXrayStatusChanged();
                            SaveState();
                        }
                        else
                        {
                            NotifyXrayStatusChanged();
                            _ = RefreshCurrentLatencyAsync();
                            _ = TestAllAiUnlockAsync();
                            _ = RefreshIpInfoAsync();
                        }
                    });
                });
                return;
            }
        }
        else
        {
            AppLogger.LogSystem("[主控逻辑] 正在关闭代理，停止 Xray 核心与系统代理...");
            IsConnected = false;
            IsSystemProxyEnabled = false;
            if (SelectedServer != null)
            {
                foreach (var node in Servers)
                {
                    node.IsConnected = false;
                }
            }
            NotifyXrayStatusChanged();
            SaveState();

            _ = Task.Run(() =>
            {
                _xrayManager.StopCore();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    NotifyXrayStatusChanged();
                    _ = RefreshIpInfoAsync();
                });
            });
        }
    }

    private void NotifyXrayStatusChanged()
    {
        OnPropertyChanged(nameof(IsXrayRunning));
        OnPropertyChanged(nameof(XrayStatusText));
        OnPropertyChanged(nameof(XrayStatusColor));
    }

    public void SaveState()
    {
        var extra = new Dictionary<string, string>
        {
            ["AllowLan"] = AllowLan.ToString(),
            ["IsSystemProxyEnabled"] = IsSystemProxyEnabled.ToString(),
            ["IsGlobalMode"] = IsGlobalMode.ToString(),
            ["IsUseProxyForSubUpdate"] = IsUseProxyForSubUpdate.ToString(),
            ["IsNotificationsEnabled"] = IsNotificationsEnabled.ToString(),
            ["CustomProxyRules"] = CustomProxyRules,
            ["CustomDirectRules"] = CustomDirectRules
        };
        _ = SqliteStorageService.SaveAsync(Subscriptions, Servers, SelectedServer?.Id, extra);
    }

    public void StopXrayCore()
    {
        SystemProxyManager.SetSystemProxy(false);
        _xrayManager.StopCore();
    }

    public async Task RefreshCurrentLatencyAsync()
    {
        if (SelectedServer == null) return;
        await TestSingleNodeLatencyAsync(SelectedServer);
    }

    public async Task TestSingleNodeLatencyAsync(ServerNode node)
    {
        if (node == null || node.IsTestingLatency) return;
        node.IsTestingLatency = true;
        var ms = await LatencyTester.TestTcpLatencyAsync(node.Address, node.Port);
        node.LatencyMs = ms > 0 ? ms : 0;
        node.IsTestingLatency = false;
    }

    public async Task TestAllNodesLatencyAsync()
    {
        if (IsTestingAllNodes || Servers.Count == 0) return;
        IsTestingAllNodes = true;
        AppLogger.LogSystem($"[节点测速] 开始并发测速全部 {Servers.Count} 个节点...");
        using var semaphore = new SemaphoreSlim(5);
        var tasks = Servers.Select(async node =>
        {
            await semaphore.WaitAsync();
            try
            {
                await TestSingleNodeLatencyAsync(node);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
        IsTestingAllNodes = false;
        AppLogger.LogSystem("[节点测速] 所有节点测速完成。");
    }

    public async Task TestSingleAiUnlockAsync(AiUnlockItem item)
    {
        if (item == null || item.IsTesting) return;
        item.IsTesting = true;
        item.Status = UnlockStatus.Testing;
        AppLogger.LogSystem($"[AI检测] 开始测试平台: {item.Name}...");
        try
        {
            var (status, ms) = await AiUnlockTester.TestPlatformAsync(item.Key, IsConnected);
            item.Status = status;
            item.LatencyMs = ms;
        }
        catch (Exception ex)
        {
            item.Status = UnlockStatus.Failed;
            item.LatencyMs = 0;
            AppLogger.LogSystem($"[AI检测] 平台 {item.Name} 测试发生异常: {ex.Message}");
        }
        finally
        {
            item.IsTesting = false;
        }
        AppLogger.LogSystem($"[AI检测] 平台 {item.Name} 测试完成: 状态={item.Status}, 延迟={item.LatencyMs}ms");
    }

    public async Task TestAllAiUnlockAsync()
    {
        if (IsTestingAiUnlock) return;
        IsTestingAiUnlock = true;
        AppLogger.LogSystem("[AI检测] 开始自动全量测试 AI 平台解锁状态...");
        try
        {
            var tasks = AiUnlockItems.Select(item => TestSingleAiUnlockAsync(item)).ToList();
            await Task.WhenAll(tasks);
        }
        finally
        {
            IsTestingAiUnlock = false;
        }
        AppLogger.LogSystem("[AI检测] 全量 AI 平台解锁状态测试完成。");
    }

    public async Task UpdateSubscriptionItemAsync(SubscriptionItem item, bool showAlert = true)
    {
        AppLogger.LogSystem($"[订阅逻辑] 开始更新订阅: {item.Name} (URL: {item.Url}, 使用代理: {IsUseProxyForSubUpdate})");
        item.IsUpdating = true;
        IsSubUpdating = true;
        try
        {
            if (IsUseProxyForSubUpdate && !_xrayManager.IsRunning)
            {
                var srv = SelectedServer ?? Servers.FirstOrDefault();
                if (srv != null)
                {
                    AppLogger.LogSystem("[订阅逻辑] 代理更新已开启，但 Xray 内核未运行，正在自动启动内核以提供代理支持...");
                    string configJson = GenerateCurrentConfig(srv);
                    _xrayManager.StartCore(configJson);
                    await Task.Delay(1000); // 等待内核就绪
                }
            }

            var fetchedNodes = await SubscriptionService.FetchSubscriptionNodesAsync(item.Url, IsUseProxyForSubUpdate);
            AppLogger.LogSystem($"[订阅逻辑] 订阅解析完成，获取到 {fetchedNodes.Count} 个节点。");
            item.NodeCount = fetchedNodes.Count > 0 ? fetchedNodes.Count : 0;
            item.LastUpdated = DateTime.Now;

            item.Nodes.Clear();
            foreach (var node in fetchedNodes)
            {
                node.SubscriptionId = item.Id;
                item.Nodes.Add(node);
            }

            if (SelectedSubscription == item)
            {
                Servers.Clear();
                foreach (var node in item.Nodes)
                {
                    Servers.Add(node);
                }
                if (SelectedServer == null || !Servers.Contains(SelectedServer))
                {
                    SelectedServer = Servers.FirstOrDefault();
                }
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

    public async Task StartSpeedTestAsync()
    {
        if (SelectedServer == null)
        {
            AppLogger.LogSystem("[节点测速] 提示：当前未选中任何节点。");
            new Views.AlertWindow("测速提示", "请先在节点列表中选择一个需要测速的节点。", false).ShowDialog();
            return;
        }

        if (IsSpeedTesting)
        {
            CancelSpeedTest();
            return;
        }

        IsSpeedTesting = true;
        SpeedTestProgress = 0;
        SpeedTestProgressText = "0.0 / 50.0 MB (0%)";
        SpeedTestCurrentSpeedText = "0 B/s";
        SpeedTestPeakSpeedText = "0 B/s";
        SpeedTestAverageSpeedText = "0 B/s";
        SpeedTestStatusText = $"正在初始化针对节点「{SelectedServer.Name}」的测速...";

        AppLogger.LogSystem($"[节点测速] 开始针对节点「{SelectedServer.Name}」({SelectedServer.Address}:{SelectedServer.Port}) 进行 CacheFly 50MB 真实下载测速...");

        _speedTestCts = new CancellationTokenSource();
        bool startedTempCore = false;

        try
        {
            if (!_xrayManager.IsRunning)
            {
                AppLogger.LogSystem("[节点测速] Xray 内核未运行，正在临时启动内核进行节点测速...");
                string configJson = GenerateCurrentConfig(SelectedServer);
                bool ok = _xrayManager.StartCore(configJson);
                if (!ok)
                {
                    SpeedTestStatusText = "测速失败: 无法启动 Xray 内核";
                    AppLogger.LogSystem("[节点测速] 错误: 无法启动 Xray 内核，测速终止。");
                    IsSpeedTesting = false;
                    return;
                }
                startedTempCore = true;

                // 智能轮询等待代理端口 10909 启动就绪 (最多等待 5 秒)
                SpeedTestStatusText = "等待 Xray 代理端口就绪...";
                bool portReady = false;
                for (int i = 0; i < 10; i++)
                {
                    if (_speedTestCts.IsCancellationRequested) break;
                    await Task.Delay(500, _speedTestCts.Token);
                    int ms = await LatencyTester.TestTcpLatencyAsync("127.0.0.1", 10909, 400);
                    if (ms > 0)
                    {
                        portReady = true;
                        break;
                    }
                }
                if (!portReady && !_speedTestCts.IsCancellationRequested)
                {
                    SpeedTestStatusText = "测速失败: 代理端口启动超时";
                    AppLogger.LogSystem("[节点测速] 错误: Xray 内核代理端口 10909 启动超时。");
                    if (startedTempCore && !IsConnected) _xrayManager.StopCore();
                    IsSpeedTesting = false;
                    return;
                }
            }

            var progress = new Progress<SpeedTestReport>(report =>
            {
                SpeedTestProgress = report.ProgressPercentage;
                SpeedTestProgressText = $"{report.DownloadedBytes / (1024.0 * 1024.0):F1} / {report.TotalBytes / (1024.0 * 1024.0):F1} MB ({report.ProgressPercentage:F0}%)";
                SpeedTestCurrentSpeedText = FormatSpeed(report.CurrentSpeedBytesPerSec);
                SpeedTestPeakSpeedText = FormatSpeed(report.PeakSpeedBytesPerSec);
                SpeedTestAverageSpeedText = FormatSpeed(report.AverageSpeedBytesPerSec);
                SpeedTestStatusText = report.StatusText;
            });

            await SpeedTester.TestDownloadSpeedAsync(
                SpeedTester.DefaultTestUrl,
                "http://127.0.0.1:10909",
                progress,
                _speedTestCts.Token);

            if (SelectedServer != null && !string.IsNullOrEmpty(SpeedTestAverageSpeedText))
            {
                AppLogger.LogSystem($"[节点测速] 节点「{SelectedServer.Name}」测速完成: 均速 {SpeedTestAverageSpeedText}, 峰值 {SpeedTestPeakSpeedText}");
            }
        }
        catch (OperationCanceledException)
        {
            SpeedTestStatusText = "测速已手动取消";
            AppLogger.LogSystem("[节点测速] 用户手动取消了测速。");
        }
        catch (Exception ex)
        {
            SpeedTestStatusText = $"测速异常: {ex.Message}";
            AppLogger.LogSystem($"[节点测速] 测速过程中发生异常: {ex.Message}");
        }
        finally
        {
            if (startedTempCore && !IsConnected)
            {
                AppLogger.LogSystem("[节点测速] 测速完成，清理临时启动的 Xray 内核服务...");
                _xrayManager.StopCore();
            }
            IsSpeedTesting = false;
            _speedTestCts?.Dispose();
            _speedTestCts = null;
        }
    }

    public void CancelSpeedTest()
    {
        if (_speedTestCts != null && !_speedTestCts.IsCancellationRequested)
        {
            _speedTestCts.Cancel();
            AppLogger.LogSystem("[节点测速] 正在请求取消测速...");
        }
    }

    public async Task RefreshIpInfoAsync()
    {
        if (IsCheckingIp) return;
        IsCheckingIp = true;
        AppLogger.LogSystem("[IP查询] 开始刷新本地 IP 与出口 IP 信息...");

        LocalLanIpText = SystemProxyManager.GetLocalLanIp();
        LocalPublicIpText = "正在查询...";
        OutboundIpText = "正在查询...";
        OutboundLocationText = "";

        try
        {
            var localTask = IpQueryService.GetLocalPublicIpAsync();

            Task<IpInfo>? outboundTask = null;
            if (_xrayManager.IsRunning)
            {
                outboundTask = IpQueryService.GetOutboundIpAsync("http://127.0.0.1:10909");
            }

            var localInfo = await localTask;
            LocalPublicIpText = string.IsNullOrEmpty(localInfo.LocationSummary) || localInfo.LocationSummary == "未知位置"
                ? localInfo.Ip
                : $"{localInfo.Ip} ({localInfo.LocationSummary})";

            if (outboundTask != null)
            {
                var outboundInfo = await outboundTask;
                OutboundIpText = outboundInfo.Ip;
                OutboundLocationText = outboundInfo.LocationSummary;
            }
            else
            {
                OutboundIpText = "未开启代理 (直连本地)";
                OutboundLocationText = "内核未启动";
            }

            AppLogger.LogSystem($"[IP查询] 查询完成: 本地LAN={LocalLanIpText}, 本地公网={LocalPublicIpText}, 出口={OutboundIpText} ({OutboundLocationText})");
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[IP查询] 查询 IP 出现异常: {ex.Message}");
        }
        finally
        {
            IsCheckingIp = false;
        }
    }

    public void SortNodesByLatency()
    {
        var sortedList = Servers
            .OrderBy(s => s.LatencyMs <= 0 ? int.MaxValue : s.LatencyMs)
            .ToList();

        Servers.Clear();
        foreach (var node in sortedList)
        {
            Servers.Add(node);
        }
        AppLogger.LogSystem("[节点列表] 已按照延迟从低到高对服务器节点进行排序。");
    }
}
