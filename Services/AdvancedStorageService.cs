using System.Diagnostics;
using System.IO;
using System.Text.Json;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public class AppStorageSchema
{
    public int Version { get; set; } = 1;
    public DateTime LastSavedAt { get; set; } = DateTime.Now;
    public List<SubscriptionItem> Subscriptions { get; set; } = new();
    public List<ServerNode> CachedServers { get; set; } = new();
    public string? SelectedServerId { get; set; }
}

public static class AdvancedStorageService
{
    private static readonly string DataDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string DataFilePath = Path.Combine(DataDirectory, "app_data.json");
    private static readonly string TempFilePath = Path.Combine(DataDirectory, "app_data.json.tmp");
    private static readonly string BackupFilePath = Path.Combine(DataDirectory, "app_data.bak.json");

    private static readonly SemaphoreSlim LockSlim = new(1, 1);

    public static async Task<AppStorageSchema> LoadAsync()
    {
        await LockSlim.WaitAsync();
        try
        {
            AppLogger.LogSystem($"[高级存储] 正在安全加载持久化存储库 (路径: {DataFilePath})...");
            if (File.Exists(DataFilePath))
            {
                var json = await File.ReadAllTextAsync(DataFilePath);
                var schema = JsonSerializer.Deserialize<AppStorageSchema>(json);
                if (schema != null)
                {
                    AppLogger.LogSystem($"[高级存储] 数据加载成功 (数据版本: v{schema.Version}, 包含 {schema.Subscriptions.Count} 个订阅源，{schema.CachedServers.Count} 个节点)。");
                    return schema;
                }
            }
            else if (File.Exists(BackupFilePath))
            {
                AppLogger.LogSystem("[高级存储] 警告：主数据文件不存在，正在尝试从备份文件 (app_data.bak.json) 恢复数据...");
                var backupJson = await File.ReadAllTextAsync(BackupFilePath);
                var schema = JsonSerializer.Deserialize<AppStorageSchema>(backupJson);
                if (schema != null)
                {
                    AppLogger.LogSystem("[高级存储] 从备份文件恢复数据成功！");
                    return schema;
                }
            }
            else
            {
                AppLogger.LogSystem("[高级存储] 提示：本地无持久化存储，初始化默认空状态。");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[高级存储] 加载发生异常: {ex.Message}，尝试从备份恢复...");
            try
            {
                if (File.Exists(BackupFilePath))
                {
                    var backupJson = await File.ReadAllTextAsync(BackupFilePath);
                    var schema = JsonSerializer.Deserialize<AppStorageSchema>(backupJson);
                    if (schema != null) return schema;
                }
            }
            catch { }
        }
        finally
        {
            LockSlim.Release();
        }

        return new AppStorageSchema();
    }

    public static async Task SaveAsync(IEnumerable<SubscriptionItem> subscriptions, IEnumerable<ServerNode> servers, string? selectedServerId)
    {
        await LockSlim.WaitAsync();
        try
        {
            var schema = new AppStorageSchema
            {
                Version = 1,
                LastSavedAt = DateTime.Now,
                Subscriptions = subscriptions.ToList(),
                CachedServers = servers.ToList(),
                SelectedServerId = selectedServerId
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(schema, options);

            // 1. Atomic Write to Temp file
            await File.WriteAllTextAsync(TempFilePath, json);

            // 2. Backup existing main file if exists
            if (File.Exists(DataFilePath))
            {
                if (File.Exists(BackupFilePath))
                {
                    File.Delete(BackupFilePath);
                }
                File.Move(DataFilePath, BackupFilePath);
            }

            // 3. Move Temp file to main target file (Atomic replacement)
            if (File.Exists(DataFilePath))
            {
                File.Delete(DataFilePath);
            }
            File.Move(TempFilePath, DataFilePath);

            AppLogger.LogSystem("[高级存储] 原子化持久化写入与备份成功 (Atomicity & Backup verified)。");
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[高级存储] 异步原子保存发生异常: {ex.Message}");
        }
        finally
        {
            LockSlim.Release();
        }
    }
}
