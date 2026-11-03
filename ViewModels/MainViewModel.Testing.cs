using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public partial class MainViewModel
{
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
