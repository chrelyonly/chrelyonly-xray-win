using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public class SqliteDataModel
{
    public List<SubscriptionItem> Subscriptions { get; set; } = new();
    public List<ServerNode> CachedServers { get; set; } = new();
    public string? SelectedServerId { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
}

public static class SqliteStorageService
{
    private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";
    private static readonly SemaphoreSlim LockSlim = new(1, 1);

    static SqliteStorageService()
    {
        InitializeDatabase();
    }

    private static void InitializeDatabase()
    {
        try
        {
            AppLogger.LogSystem($"[SQLite存储] 正在初始化 SQLite 数据库，路径: {DbPath}");
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Subscriptions (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    Url TEXT,
                    NodeCount INTEGER,
                    LastUpdated TEXT,
                    AutoUpdate INTEGER
                );

                CREATE TABLE IF NOT EXISTS Servers (
                    Id TEXT PRIMARY KEY,
                    SubscriptionId TEXT,
                    Name TEXT,
                    Address TEXT,
                    Port INTEGER,
                    Protocol TEXT,
                    Security TEXT,
                    Transport TEXT,
                    Uuid TEXT,
                    Sni TEXT,
                    PublicKey TEXT,
                    ShortId TEXT,
                    Flow TEXT,
                    LatencyMs INTEGER,
                    IsOpenAiUnlocked INTEGER,
                    IsClaudeUnlocked INTEGER,
                    IsGeminiUnlocked INTEGER
                );

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                );
            ";
            command.ExecuteNonQuery();

            // 检查并自动适配旧版数据库：若 Servers 表缺少 SubscriptionId 字段则动态添加
            try
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Servers ADD COLUMN SubscriptionId TEXT;";
                alterCmd.ExecuteNonQuery();
                AppLogger.LogSystem("[SQLite存储] 成功为 Servers 表补全 SubscriptionId 列。");
            }
            catch
            {
                // 列已存在或无需添加，忽略异常
            }

            AppLogger.LogSystem("[SQLite存储] SQLite 数据库表结构初始化成功。");
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[SQLite存储] 初始化数据库发生异常: {ex.Message}");
        }
    }

    public static async Task<SqliteDataModel> LoadAsync()
    {
        await LockSlim.WaitAsync();
        var model = new SqliteDataModel();
        try
        {
            AppLogger.LogSystem("[SQLite存储] 正在从 SQLite 数据库加载持久化数据...");
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            // 1. Load Subscriptions
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, Name, Url, NodeCount, LastUpdated, AutoUpdate FROM Subscriptions";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var sub = new SubscriptionItem
                    {
                        Id = reader.GetString(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Url = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        NodeCount = reader.GetInt32(3),
                        LastUpdated = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                        AutoUpdate = reader.GetInt32(5) == 1
                    };
                    model.Subscriptions.Add(sub);
                }
            }

            if (model.Subscriptions.Count == 0)
            {
                var defaultSub = new SubscriptionItem
                {
                };
                model.Subscriptions.Add(defaultSub);
            }

            // 2. Load Servers
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, SubscriptionId, Name, Address, Port, Protocol, Security, Transport, Uuid, Sni, PublicKey, ShortId, Flow, LatencyMs, IsOpenAiUnlocked, IsClaudeUnlocked, IsGeminiUnlocked FROM Servers";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var node = new ServerNode
                    {
                        Id = reader.GetString(0),
                        SubscriptionId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Address = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Port = reader.GetInt32(4),
                        Protocol = reader.IsDBNull(5) ? "VLESS" : reader.GetString(5),
                        Security = reader.IsDBNull(6) ? "Reality" : reader.GetString(6),
                        Transport = reader.IsDBNull(7) ? "TCP" : reader.GetString(7),
                        Uuid = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        Sni = reader.IsDBNull(9) ? "" : reader.GetString(9),
                        PublicKey = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        ShortId = reader.IsDBNull(11) ? "" : reader.GetString(11),
                        Flow = reader.IsDBNull(12) ? "" : reader.GetString(12),
                        LatencyMs = reader.GetInt32(13),
                        IsOpenAiUnlocked = reader.GetInt32(14) == 1,
                        IsClaudeUnlocked = reader.GetInt32(15) == 1,
                        IsGeminiUnlocked = reader.GetInt32(16) == 1
                    };
                    model.CachedServers.Add(node);

                    var sub = model.Subscriptions.FirstOrDefault(s => s.Id == node.SubscriptionId);
                    if (sub != null)
                    {
                        sub.Nodes.Add(node);
                    }
                    else if (model.Subscriptions.Count > 0)
                    {
                        model.Subscriptions[0].Nodes.Add(node);
                    }
                }
            }

            // 3. Load Settings
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Key, Value FROM Settings";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string k = reader.GetString(0);
                    string v = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    model.Settings[k] = v;
                    if (k == "SelectedServerId")
                    {
                        model.SelectedServerId = v;
                    }
                }
            }

            AppLogger.LogSystem($"[SQLite存储] 数据加载成功：包含 {model.Subscriptions.Count} 个订阅源，{model.CachedServers.Count} 个缓存节点，{model.Settings.Count} 项设置。");
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[SQLite存储] 加载数据发生异常: {ex.Message}");
        }
        finally
        {
            LockSlim.Release();
        }

        return model;
    }

    public static async Task SaveAsync(IEnumerable<SubscriptionItem> subscriptions, IEnumerable<ServerNode> ignoredServers, string? selectedServerId, Dictionary<string, string>? extraSettings = null)
    {
        await LockSlim.WaitAsync();
        try
        {
            AppLogger.LogSystem("[SQLite存储] 正在向 SQLite 数据库持久化保存事务...");
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                // Clear old data
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM Subscriptions; DELETE FROM Servers;";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert Subscriptions and their Nodes
                foreach (var sub in subscriptions)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Subscriptions (Id, Name, Url, NodeCount, LastUpdated, AutoUpdate)
                        VALUES (@Id, @Name, @Url, @NodeCount, @LastUpdated, @AutoUpdate);
                    ";
                    cmd.Parameters.AddWithValue("@Id", sub.Id ?? Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue("@Name", sub.Name ?? "");
                    cmd.Parameters.AddWithValue("@Url", sub.Url ?? "");
                    cmd.Parameters.AddWithValue("@NodeCount", sub.Nodes.Count > 0 ? sub.Nodes.Count : sub.NodeCount);
                    cmd.Parameters.AddWithValue("@LastUpdated", sub.LastUpdated.HasValue ? sub.LastUpdated.Value.ToString("o") : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AutoUpdate", sub.AutoUpdate ? 1 : 0);
                    await cmd.ExecuteNonQueryAsync();

                    foreach (var node in sub.Nodes)
                    {
                        using var sCmd = connection.CreateCommand();
                        sCmd.Transaction = transaction;
                        sCmd.CommandText = @"
                            INSERT INTO Servers (Id, SubscriptionId, Name, Address, Port, Protocol, Security, Transport, Uuid, Sni, PublicKey, ShortId, Flow, LatencyMs, IsOpenAiUnlocked, IsClaudeUnlocked, IsGeminiUnlocked)
                            VALUES (@Id, @SubscriptionId, @Name, @Address, @Port, @Protocol, @Security, @Transport, @Uuid, @Sni, @PublicKey, @ShortId, @Flow, @LatencyMs, @IsOpenAiUnlocked, @IsClaudeUnlocked, @IsGeminiUnlocked);
                        ";
                        sCmd.Parameters.AddWithValue("@Id", node.Id ?? Guid.NewGuid().ToString());
                        sCmd.Parameters.AddWithValue("@SubscriptionId", sub.Id);
                        sCmd.Parameters.AddWithValue("@Name", node.Name ?? "");
                        sCmd.Parameters.AddWithValue("@Address", node.Address ?? "");
                        sCmd.Parameters.AddWithValue("@Port", node.Port);
                        sCmd.Parameters.AddWithValue("@Protocol", node.Protocol ?? "VLESS");
                        sCmd.Parameters.AddWithValue("@Security", node.Security ?? "Reality");
                        sCmd.Parameters.AddWithValue("@Transport", node.Transport ?? "TCP");
                        sCmd.Parameters.AddWithValue("@Uuid", node.Uuid ?? "");
                        sCmd.Parameters.AddWithValue("@Sni", node.Sni ?? "");
                        sCmd.Parameters.AddWithValue("@PublicKey", node.PublicKey ?? "");
                        sCmd.Parameters.AddWithValue("@ShortId", node.ShortId ?? "");
                        sCmd.Parameters.AddWithValue("@Flow", node.Flow ?? "");
                        sCmd.Parameters.AddWithValue("@LatencyMs", node.LatencyMs);
                        sCmd.Parameters.AddWithValue("@IsOpenAiUnlocked", node.IsOpenAiUnlocked ? 1 : 0);
                        sCmd.Parameters.AddWithValue("@IsClaudeUnlocked", node.IsClaudeUnlocked ? 1 : 0);
                        sCmd.Parameters.AddWithValue("@IsGeminiUnlocked", node.IsGeminiUnlocked ? 1 : 0);
                        await sCmd.ExecuteNonQueryAsync();
                    }
                }

                // Save SelectedServerId Setting
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Settings (Key, Value) VALUES ('SelectedServerId', @Val)
                        ON CONFLICT(Key) DO UPDATE SET Value = @Val;
                    ";
                    cmd.Parameters.AddWithValue("@Val", selectedServerId ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Save Extra Settings
                if (extraSettings != null)
                {
                    foreach (var kvp in extraSettings)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            INSERT INTO Settings (Key, Value) VALUES (@Key, @Val)
                            ON CONFLICT(Key) DO UPDATE SET Value = @Val;
                        ";
                        cmd.Parameters.AddWithValue("@Key", kvp.Key);
                        cmd.Parameters.AddWithValue("@Val", kvp.Value ?? "");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                AppLogger.LogSystem("[SQLite存储] SQLite 事务提交成功，数据持久化完成。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[SQLite存储] 保存数据发生异常: {ex.Message}");
        }
        finally
        {
            LockSlim.Release();
        }
    }
}
