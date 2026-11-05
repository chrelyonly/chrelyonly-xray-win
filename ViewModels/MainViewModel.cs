using System.Collections.ObjectModel;
using System.Windows.Media;
using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public partial class MainViewModel : ViewModelBase
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

    // Auto Select Fields
    private int _autoSelectIntervalSeconds = 30;
    private ServerNode? _activeAutoSelectedServer;
    private CancellationTokenSource? _autoSelectCts;

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

    private double _uiScale = 1.0;
    private double _savedWindowWidth = 480;
    private double _savedWindowHeight = 760;
    private double _cardOpacity = 0.85;
    private SolidColorBrush _cardBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(217, 255, 255, 255));

    public double UiScale
    {
        get => _uiScale;
        set
        {
            double clamped = Math.Clamp(value, 0.75, 2.0);
            if (SetProperty(ref _uiScale, clamped))
            {
                OnPropertyChanged(nameof(UiScalePercentText));
                SaveState();
            }
        }
    }

    public string UiScalePercentText => $"{Math.Round(UiScale * 100)}%";

    public double CardOpacity
    {
        get => _cardOpacity;
        set
        {
            double clamped = Math.Clamp(value, 0.3, 1.0);
            if (SetProperty(ref _cardOpacity, clamped))
            {
                OnPropertyChanged(nameof(CardOpacityPercentText));
                _cardBackgroundBrush.Color = System.Windows.Media.Color.FromArgb((byte)(clamped * 255), 255, 255, 255);
                OnPropertyChanged(nameof(CardBackgroundBrush));
                SaveState();
            }
        }
    }

    public string CardOpacityPercentText => $"{Math.Round(CardOpacity * 100)}%";

    public SolidColorBrush CardBackgroundBrush => _cardBackgroundBrush;

    public double SavedWindowWidth
    {
        get => _savedWindowWidth;
        set => SetProperty(ref _savedWindowWidth, value);
    }

    public double SavedWindowHeight
    {
        get => _savedWindowHeight;
        set => SetProperty(ref _savedWindowHeight, value);
    }

    public Action? ResetDefaultSizeAction { get; set; }

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

    public string AppVersionText => $"v{Services.UpdateCheckService.CurrentVersionCode}修仙版喵~";

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

    public int AutoSelectIntervalSeconds
    {
        get => _autoSelectIntervalSeconds;
        set
        {
            if (SetProperty(ref _autoSelectIntervalSeconds, value))
            {
                OnPropertyChanged(nameof(AutoSelectIntervalText));
                AppLogger.LogSystem($"[自动选择] 检测间隔已更改为: {value} 秒");
                if (!_isInitializing) SaveState();
            }
        }
    }

    public string AutoSelectIntervalText
    {
        get => _autoSelectIntervalSeconds.ToString();
        set
        {
            if (int.TryParse(value, out int parsed) && parsed >= 5)
            {
                AutoSelectIntervalSeconds = parsed;
            }
        }
    }

    public ServerNode? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                if (value != null && value.IsAutoSelectNode)
                {
                    StartAutoSelectLoop();
                }
                else
                {
                    StopAutoSelectLoop();
                }

                if (!_isInitializing)
                {
                    SaveState();
                }
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
                        ShowModalDialog(new Views.AlertWindow("代理冲突提示", $"检测到当前 Windows 系统代理已被其他程序占用 ({proxyServer})。", true));
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
                    var autoNode = GetOrCreateAutoSelectNode();
                    Servers.Add(autoNode);

                    foreach (var node in value.Nodes)
                    {
                        if (!node.IsAutoSelectNode)
                        {
                            Servers.Add(node);
                        }
                    }
                    AppLogger.LogSystem($"[订阅逻辑] 切换订阅源 -> {value.Name}，已加载 {value.Nodes.Count} 个节点。");

                    if (SelectedServer == null || !Servers.Contains(SelectedServer))
                    {
                        SelectedServer = autoNode;
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
    public RelayCommand PasteImportNodeCommand { get; }
    public RelayCommand CopyNodeLinkCommand { get; }

    // Traffic, Speed Test & IP Commands
    public RelayCommand ResetTrafficCommand { get; }
    public RelayCommand StartSpeedTestCommand { get; }
    public RelayCommand CancelSpeedTestCommand { get; }
    public RelayCommand RefreshIpInfoCommand { get; }

    // Routing Rules Commands
    public RelayCommand SaveRulesCommand { get; }
    public RelayCommand ResetRulesCommand { get; }

    // Version Check Command
    public RelayCommand CheckUpdateCommand { get; }

    // UI Zoom & Size Commands
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }
    public RelayCommand ResetDefaultSizeCommand { get; }

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

        // Find the subscription that owns the saved SelectedServerId, or fallback to saved SelectedSubscriptionId
        SubscriptionItem? targetSub = null;
        if (!string.IsNullOrEmpty(savedData.SelectedServerId))
        {
            targetSub = Subscriptions.FirstOrDefault(sub => sub.Nodes.Any(n => n.Id == savedData.SelectedServerId));
        }

        if (targetSub == null && savedData.Settings.TryGetValue("SelectedSubscriptionId", out string? savedSubId) && !string.IsNullOrEmpty(savedSubId))
        {
            targetSub = Subscriptions.FirstOrDefault(sub => sub.Id == savedSubId);
        }

        if (targetSub == null && Subscriptions.Count > 0)
        {
            targetSub = Subscriptions[0];
        }

        if (savedData.Settings.TryGetValue("AutoSelectIntervalSeconds", out string? autoIntStr) && int.TryParse(autoIntStr, out int savedAutoInt) && savedAutoInt >= 5)
        {
            _autoSelectIntervalSeconds = savedAutoInt;
        }

        if (targetSub != null)
        {
            _selectedSubscription = targetSub;
            Servers.Clear();
            var autoNode = GetOrCreateAutoSelectNode();
            Servers.Add(autoNode);

            foreach (var node in targetSub.Nodes)
            {
                if (!node.IsAutoSelectNode)
                {
                    Servers.Add(node);
                }
            }

            if (!string.IsNullOrEmpty(savedData.SelectedServerId))
            {
                _selectedServer = Servers.FirstOrDefault(s => s.Id == savedData.SelectedServerId);
            }

            if (_selectedServer == null)
            {
                _selectedServer = autoNode;
            }

            if (_selectedServer.IsAutoSelectNode)
            {
                StartAutoSelectLoop();
            }
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
        if (savedData.Settings.TryGetValue("UiScale", out string? scaleStr) && double.TryParse(scaleStr, out double savedScale))
        {
            _uiScale = Math.Clamp(savedScale, 0.75, 2.0);
        }
        if (savedData.Settings.TryGetValue("CardOpacity", out string? cardOpStr) && double.TryParse(cardOpStr, out double savedCardOp))
        {
            _cardOpacity = Math.Clamp(savedCardOp, 0.3, 1.0);
            _cardBackgroundBrush.Color = System.Windows.Media.Color.FromArgb((byte)(_cardOpacity * 255), 255, 255, 255);
        }
        if (savedData.Settings.TryGetValue("WindowWidth", out string? wStr) && double.TryParse(wStr, out double savedW))
        {
            _savedWindowWidth = savedW;
        }
        if (savedData.Settings.TryGetValue("WindowHeight", out string? hStr) && double.TryParse(hStr, out double savedH))
        {
            _savedWindowHeight = savedH;
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
            ShowModalDialog(new Views.AlertWindow("规则已保存", "自定义分流规则已保存成功，并已即时生效！", true));
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
            ShowModalDialog(new Views.AlertWindow("规则重置", "已成功重置所有自定义分流规则。", true));
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

        PasteImportNodeCommand = new RelayCommand(() =>
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    ShowModalDialog(new Views.AlertWindow("粘贴导入失败", "剪贴板中没有文本内容，请先复制节点分享链接（如 vless://...）。", false));
                    return;
                }
                string clipText = Clipboard.GetText().Trim();
                var node = ProxyLinkParser.ParseShareLink(clipText);
                if (node == null)
                {
                    var lines = clipText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        node = ProxyLinkParser.ParseShareLink(line.Trim());
                        if (node != null) break;
                    }
                }

                if (node == null)
                {
                    ShowModalDialog(new Views.AlertWindow("粘贴导入失败", "无法从剪贴板解析出有效的代理节点链接（支持 vless://, hysteria2:// 等）。", false));
                    return;
                }

                var fixedSub = Subscriptions.FirstOrDefault(s => s.IsFixed || s.Id == "fixed-general-subscription");
                if (fixedSub == null)
                {
                    fixedSub = new SubscriptionItem
                    {
                        Id = "fixed-general-subscription",
                        Name = "通用/自定义订阅 (内置)",
                        Url = "local://general",
                        IsFixed = true
                    };
                    Subscriptions.Insert(0, fixedSub);
                }

                node.SubscriptionId = fixedSub.Id;
                fixedSub.Nodes.Add(node);
                fixedSub.NodeCount = fixedSub.Nodes.Count;
                fixedSub.LastUpdated = DateTime.Now;

                if (SelectedSubscription == fixedSub)
                {
                    Servers.Add(node);
                    if (SelectedServer == null)
                    {
                        SelectedServer = node;
                    }
                }

                SaveState();
                AppLogger.LogSystem($"[节点导入] 成功通过剪贴板导入节点: {node.Name} ({node.Protocol} - {node.Address}:{node.Port}) 到固定通用订阅。");
                ShowModalDialog(new Views.AlertWindow("导入成功", $"成功导入节点到【{fixedSub.Name}】：\n\n🔹 名称: {node.Name}\n🔹 协议: {node.Protocol}\n🔹 地址: {node.Address}:{node.Port}", true));
            }
            catch (Exception ex)
            {
                ShowModalDialog(new Views.AlertWindow("导入错误", $"粘贴导入节点时发生异常: {ex.Message}", false));
            }
        });

        CopyNodeLinkCommand = new RelayCommand(obj =>
        {
            var node = obj as ServerNode ?? SelectedServer;
            if (node == null) return;
            try
            {
                string link = ProxyLinkParser.ExportShareLink(node);
                Clipboard.SetText(link);
                AppLogger.LogSystem($"[节点导出] 已成功复制节点分享链接到剪贴板: {node.Name}");
                ShowModalDialog(new Views.AlertWindow("导出成功", $"已成功将节点分享链接复制到剪贴板！\n\n节点名称: {node.Name}\n链接: {link}", true));
            }
            catch (Exception ex)
            {
                ShowModalDialog(new Views.AlertWindow("导出失败", $"复制节点链接失败: {ex.Message}", false));
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
        CheckUpdateCommand = new RelayCommand(async () => await CheckForUpdateAsync(manual: true));
        ZoomInCommand = new RelayCommand(() => UiScale += 0.1);
        ZoomOutCommand = new RelayCommand(() => UiScale -= 0.1);
        ResetZoomCommand = new RelayCommand(() => UiScale = 1.0);
        ResetDefaultSizeCommand = new RelayCommand(() =>
        {
            UiScale = 1.0;
            ResetDefaultSizeAction?.Invoke();
            AppLogger.LogSystem("[系统设置] 已恢复默认窗口大小与 100% 缩放比例。");
        });

        // 启动时初始化测速、IP 查询与自动检测新版本（不自动更新订阅，仅支持手动更新）
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

            // 自动检测应用新版本
            await Task.Delay(1000);
            await CheckForUpdateAsync(manual: false);
        });
    }

    public void SaveState()
    {
        if (_isInitializing) return;

        var extra = new Dictionary<string, string>
        {
            ["SelectedSubscriptionId"] = SelectedSubscription?.Id ?? "",
            ["AutoSelectIntervalSeconds"] = AutoSelectIntervalSeconds.ToString(),
            ["AllowLan"] = AllowLan.ToString(),
            ["IsSystemProxyEnabled"] = IsSystemProxyEnabled.ToString(),
            ["IsGlobalMode"] = IsGlobalMode.ToString(),
            ["IsUseProxyForSubUpdate"] = IsUseProxyForSubUpdate.ToString(),
            ["IsNotificationsEnabled"] = IsNotificationsEnabled.ToString(),
            ["CustomProxyRules"] = CustomProxyRules,
            ["CustomDirectRules"] = CustomDirectRules,
            ["UiScale"] = UiScale.ToString("F2"),
            ["CardOpacity"] = CardOpacity.ToString("F2"),
            ["WindowWidth"] = SavedWindowWidth.ToString("F0"),
            ["WindowHeight"] = SavedWindowHeight.ToString("F0")
        };
        _ = SqliteStorageService.SaveAsync(Subscriptions, Servers, SelectedServer?.Id, extra);
    }

    private ServerNode GetOrCreateAutoSelectNode()
    {
        return new ServerNode
        {
            Id = "auto-select-node-fixed",
            Name = "⚡ 自动选择",
            Protocol = "AUTO",
            Address = "自动检测并切至最低延迟节点",
            Port = 0,
            LatencyMs = -1
        };
    }

    private void StartAutoSelectLoop()
    {
        StopAutoSelectLoop();
        _autoSelectCts = new CancellationTokenSource();
        var token = _autoSelectCts.Token;

        _ = Task.Run(async () =>
        {
            AppLogger.LogSystem($"[自动选择] 已启动节点自动选择检测 (检测间隔: {AutoSelectIntervalSeconds} 秒)");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await PerformAutoSelectCheckAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[自动选择] 检测发生异常: {ex.Message}");
                }

                try
                {
                    await Task.Delay(AutoSelectIntervalSeconds * 1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            AppLogger.LogSystem("[自动选择] 已停止节点自动选择检测。");
        }, token);
    }

    private void StopAutoSelectLoop()
    {
        _autoSelectCts?.Cancel();
        _autoSelectCts = null;
    }

    public async Task PerformAutoSelectCheckAsync()
    {
        var realNodes = Servers.Where(n => !n.IsAutoSelectNode).ToList();
        if (realNodes.Count == 0) return;

        var tasks = realNodes.Select(node => TestSingleNodeLatencyAsync(node)).ToArray();
        await Task.WhenAll(tasks);

        var bestNode = realNodes
            .Where(n => n.LatencyMs > 0 && n.LatencyMs < 9999)
            .OrderBy(n => n.LatencyMs)
            .FirstOrDefault();

        if (bestNode == null)
        {
            bestNode = realNodes.FirstOrDefault();
        }

        if (bestNode != null)
        {
            bool changed = _activeAutoSelectedServer != bestNode;
            _activeAutoSelectedServer = bestNode;

            var autoNode = Servers.FirstOrDefault(n => n.IsAutoSelectNode);
            if (autoNode != null)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    autoNode.Address = $"当前最优: {bestNode.Name} ({bestNode.LatencyDisplay})";
                    autoNode.LatencyMs = bestNode.LatencyMs;
                });
            }

            if (changed && IsConnected)
            {
                AppLogger.LogSystem($"[自动选择] 最优节点更新为: {bestNode.Name} ({bestNode.Address}:{bestNode.Port}, 延迟: {bestNode.LatencyDisplay})");
                ReapplyXrayConfigIfConnected(bestNode);
            }
        }
    }

    public void StopXrayCore()
    {
        SystemProxyManager.SetSystemProxy(false);
        _xrayManager.StopCore();
    }

    public bool? ShowModalDialog(System.Windows.Window win)
    {
        return System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsModalOpen = true;
            try
            {
                if (win.Owner == null && System.Windows.Application.Current.MainWindow != null && System.Windows.Application.Current.MainWindow != win)
                {
                    win.Owner = System.Windows.Application.Current.MainWindow;
                }
                return win.ShowDialog();
            }
            finally
            {
                IsModalOpen = false;
            }
        });
    }
}
