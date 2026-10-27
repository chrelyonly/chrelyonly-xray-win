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
    private bool _isTestingAiUnlock = false;
    private bool _isTestingAllNodes = false;

    private bool _allowLan = false;
    private bool _isSystemProxyEnabled = false;
    private bool _isGlobalMode = false;
    private string _memoryUsageText = "0.0 MB";

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

                if (IsConnected && SelectedServer != null)
                {
                    AppLogger.LogSystem($"[局域网共享] 局域网共享已切换为: {value}，正在重新应用 Xray 监听地址配置...");
                    string configJson = _xrayManager.GenerateClientConfig(SelectedServer, value, IsGlobalMode);
                    _xrayManager.StartCore(configJson);
                    NotifyXrayStatusChanged();
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

                if (IsConnected && SelectedServer != null)
                {
                    AppLogger.LogSystem($"[路由模式] 路由模式已变更为: {(value ? "全局代理" : "智能分流")}，正在热重启 Xray 内核应用路由配置...");
                    string configJson = _xrayManager.GenerateClientConfig(SelectedServer, AllowLan, value);
                    _xrayManager.StartCore(configJson);
                    NotifyXrayStatusChanged();
                }
            }
        }
    }

    public string RoutingModeStatusText => IsGlobalMode ? "全局代理模式 (所有流量走代理)" : "智能分流模式 (大陆直连/海外代理)";

    public string AppVersionText => "v1.0.0 (Xray-Core v25.1)";

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
    public string XrayStatusText => IsXrayRunning ? "Xray 内核状态: 运行中" : "Xray 内核状态: 未运行";
    public string XrayStatusColor => IsXrayRunning ? "#10B981" : "#EF4444";

    public bool IsSystemProxyEnabled
    {
        get => _isSystemProxyEnabled;
        set
        {
            if (value)
            {
                var (isProxyActive, proxyServer, isOwn) = SystemProxyManager.CheckSystemProxyConflict(10809);
                if (isProxyActive && !isOwn)
                {
                    AppLogger.LogSystem($"[系统代理冲突] 检测到当前系统代理已被第三方程序占用: {proxyServer}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        new Views.AlertWindow("代理冲突提示", $"检测到当前 Windows 系统代理已被其他程序占用 ({proxyServer})。\n现已自动覆盖并切换为本程序代理 (127.0.0.1:10809)。", true).ShowDialog();
                    });
                }
            }

            if (SetProperty(ref _isSystemProxyEnabled, value))
            {
                OnPropertyChanged(nameof(SystemProxyStatusText));
                OnPropertyChanged(nameof(SystemProxyStatusColor));
                SystemProxyManager.SetSystemProxy(value, "127.0.0.1:10809");
                SaveState();
            }
        }
    }

    public string SystemProxyStatusText => IsSystemProxyEnabled ? "系统代理状态: 已开启" : "系统代理状态: 已关闭";
    public string SystemProxyStatusColor => IsSystemProxyEnabled ? "#10B981" : "#6B7280";

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

    // AI & Latency Testing Commands
    public RelayCommand TestAiUnlockItemCommand { get; }
    public RelayCommand TestAllAiUnlockCommand { get; }
    public RelayCommand TestNodeLatencyCommand { get; }
    public RelayCommand TestAllNodesLatencyCommand { get; }

    // Subscription Dialog Commands
    public RelayCommand AddSubDialogCommand { get; }
    public RelayCommand EditSubDialogCommand { get; }
    public RelayCommand DeleteSubDialogCommand { get; }
    public RelayCommand UpdateSubscriptionCommand { get; }

    public MainViewModel()
    {
        AppLogger.LogSystem("[主控逻辑] 应用程序正在初始化 ViewModel...");

        // Initialize AI Platforms
        AiUnlockItems.Add(new AiUnlockItem { Key = "OpenAI", Name = "OpenAI", TestUrl = "https://chatgpt.com" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "Claude", Name = "Claude", TestUrl = "https://claude.ai" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "Gemini", Name = "Gemini", TestUrl = "https://gemini.google.com" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "YouTube", Name = "YouTube", TestUrl = "https://www.youtube.com" });
        AiUnlockItems.Add(new AiUnlockItem { Key = "Google", Name = "Google", TestUrl = "https://www.google.com" });

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

        // Start Memory Usage Timer
        var memTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        memTimer.Tick += (s, e) => UpdateMemoryUsage();
        memTimer.Start();
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

                // If currently connected, restart core with new node config, AllowLan and IsGlobalMode
                if (IsConnected)
                {
                    AppLogger.LogSystem("[主控逻辑] 当前处于连接状态，正在热重启 Xray 内核应用新节点配置...");
                    string configJson = _xrayManager.GenerateClientConfig(node, AllowLan, IsGlobalMode);
                    _xrayManager.StartCore(configJson);
                    NotifyXrayStatusChanged();

                    // 切换节点时主动发起测速与AI解锁检测
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

        // 默认打开时自动在后台静默更新订阅并触发初始化测速
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500); // 延迟 1.5 秒等待主窗口完全初始化
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await RefreshCurrentLatencyAsync();
                if (IsConnected)
                {
                    await TestAllAiUnlockAsync();
                }
            });

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
                AppLogger.LogSystem($"[主控逻辑] 正在使用节点启动代理: {SelectedServer.Name} (允许局域网: {AllowLan}, 全局模式: {IsGlobalMode})");
                string configJson = _xrayManager.GenerateClientConfig(SelectedServer, AllowLan, IsGlobalMode);
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
        IsSystemProxyEnabled = willBeConnected;
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

        // 当链接节点成功后，主动触发测速与AI解锁检测
        if (IsConnected)
        {
            _ = RefreshCurrentLatencyAsync();
            _ = TestAllAiUnlockAsync();
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
            ["IsGlobalMode"] = IsGlobalMode.ToString()
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
