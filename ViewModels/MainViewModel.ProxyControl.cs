using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public partial class MainViewModel
{
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

    private void ToggleConnection()
    {
        bool willBeConnected = !IsConnected;
        AppLogger.LogSystem($"[主控逻辑] 触发连接开关，目标连接状态: {willBeConnected}");
        if (willBeConnected)
        {
            if (SelectedServer == null)
            {
                AppLogger.LogSystem("[主控逻辑] 提示：当前未选中任何节点。");
                ShowModalDialog(new Views.AlertWindow("提示", "请先在节点列表中选择一个服务器节点。", false));
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
                            ShowModalDialog(new Views.AlertWindow("启动失败", "未能成功启动 Xray 核心，请检查 Core/xraylib.exe 是否存在。", false));
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
}
