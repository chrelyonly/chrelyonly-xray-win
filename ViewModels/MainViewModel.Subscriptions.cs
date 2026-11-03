using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public partial class MainViewModel
{
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
                ShowModalDialog(new Views.AlertWindow("订阅更新", $"更新订阅成功！当前订阅包含 {fetchedNodes.Count} 个可用节点。", true));
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[订阅逻辑] 更新订阅发生异常: {ex.Message}");
            if (showAlert)
            {
                ShowModalDialog(new Views.AlertWindow("订阅更新失败", $"更新订阅失败，请检查网络或订阅地址是否正确。\n错误信息: {ex.Message}", false));
            }
        }
        finally
        {
            item.IsUpdating = false;
            IsSubUpdating = false;
        }
    }
}
