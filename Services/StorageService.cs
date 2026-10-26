using System.IO;
using System.Text.Json;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public class AppDataModel
{
    public List<SubscriptionItem> Subscriptions { get; set; } = new();
    public List<ServerNode> CachedServers { get; set; } = new();
    public string? SelectedServerId { get; set; }
}

public static class StorageService
{
    private static readonly string StorageFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data.json");

    public static AppDataModel Load()
    {
        try
        {
            AppLogger.LogSystem($"[数据存储] 正在从路径加载持久化数据: {StorageFilePath}");
            if (File.Exists(StorageFilePath))
            {
                var json = File.ReadAllText(StorageFilePath);
                var data = JsonSerializer.Deserialize<AppDataModel>(json);
                if (data != null)
                {
                    AppLogger.LogSystem($"[数据存储] 数据加载成功：包含 {data.Subscriptions.Count} 个订阅源，{data.CachedServers.Count} 个缓存节点。");
                    return data;
                }
            }
            else
            {
                AppLogger.LogSystem("[数据存储] 提示：本地暂无持久化数据文件 (app_data.json)，将以空状态初始化。");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[数据存储] 数据加载异常: {ex.Message}");
        }
        return new AppDataModel();
    }

    public static void Save(IEnumerable<SubscriptionItem> subscriptions, IEnumerable<ServerNode> servers, string? selectedServerId)
    {
        try
        {
            AppLogger.LogSystem($"[数据存储] 正在将状态持久化保存到: {StorageFilePath}...");
            var data = new AppDataModel
            {
                Subscriptions = subscriptions.ToList(),
                CachedServers = servers.ToList(),
                SelectedServerId = selectedServerId
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(StorageFilePath, json);
            AppLogger.LogSystem("[数据存储] 数据持久化保存成功。");
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[数据存储] 数据保存异常: {ex.Message}");
        }
    }
}
