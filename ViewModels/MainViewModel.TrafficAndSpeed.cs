using System.Net.NetworkInformation;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public partial class MainViewModel
{
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
}
