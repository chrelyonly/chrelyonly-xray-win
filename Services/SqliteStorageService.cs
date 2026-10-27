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

            // 2. Load Servers
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, Name, Address, Port, Protocol, Security, Transport, Uuid, Sni, PublicKey, ShortId, Flow, LatencyMs, IsOpenAiUnlocked, IsClaudeUnlocked, IsGeminiUnlocked FROM Servers";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var node = new ServerNode
                    {
                        Id = reader.GetString(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Address = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Port = reader.GetInt32(3),
                        Protocol = reader.IsDBNull(4) ? "VLESS" : reader.GetString(4),
                        Security = reader.IsDBNull(5) ? "Reality" : reader.GetString(5),
                        Transport = reader.IsDBNull(6) ? "TCP" : reader.GetString(6),
                        Uuid = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        Sni = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        PublicKey = reader.IsDBNull(9) ? "" : reader.GetString(9),
                        ShortId = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        Flow = reader.IsDBNull(11) ? "" : reader.GetString(11),
                        LatencyMs = reader.GetInt32(12),
                        IsOpenAiUnlocked = reader.GetInt32(13) == 1,
                        IsClaudeUnlocked = reader.GetInt32(14) == 1,
                        IsGeminiUnlocked = reader.GetInt32(15) == 1
                    };
                    model.CachedServers.Add(node);
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

    public static async Task SaveAsync(IEnumerable<SubscriptionItem> subscriptions, IEnumerable<ServerNode> servers, string? selectedServerId, Dictionary<string, string>? extraSettings = null)
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

                // Insert Subscriptions
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
                    cmd.Parameters.AddWithValue("@NodeCount", sub.NodeCount);
                    cmd.Parameters.AddWithValue("@LastUpdated", sub.LastUpdated.HasValue ? sub.LastUpdated.Value.ToString("o") : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AutoUpdate", sub.AutoUpdate ? 1 : 0);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert Servers
                foreach (var node in servers)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Servers (Id, Name, Address, Port, Protocol, Security, Transport, Uuid, Sni, PublicKey, ShortId, Flow, LatencyMs, IsOpenAiUnlocked, IsClaudeUnlocked, IsGeminiUnlocked)
                        VALUES (@Id, @Name, @Address, @Port, @Protocol, @Security, @Transport, @Uuid, @Sni, @PublicKey, @ShortId, @Flow, @LatencyMs, @IsOpenAiUnlocked, @IsClaudeUnlocked, @IsGeminiUnlocked);
                    ";
                    cmd.Parameters.AddWithValue("@Id", node.Id ?? Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue("@Name", node.Name ?? "");
                    cmd.Parameters.AddWithValue("@Address", node.Address ?? "");
                    cmd.Parameters.AddWithValue("@Port", node.Port);
                    cmd.Parameters.AddWithValue("@Protocol", node.Protocol ?? "VLESS");
                    cmd.Parameters.AddWithValue("@Security", node.Security ?? "Reality");
                    cmd.Parameters.AddWithValue("@Transport", node.Transport ?? "TCP");
                    cmd.Parameters.AddWithValue("@Uuid", node.Uuid ?? "");
                    cmd.Parameters.AddWithValue("@Sni", node.Sni ?? "");
                    cmd.Parameters.AddWithValue("@PublicKey", node.PublicKey ?? "");
                    cmd.Parameters.AddWithValue("@ShortId", node.ShortId ?? "");
                    cmd.Parameters.AddWithValue("@Flow", node.Flow ?? "");
                    cmd.Parameters.AddWithValue("@LatencyMs", node.LatencyMs);
                    cmd.Parameters.AddWithValue("@IsOpenAiUnlocked", node.IsOpenAiUnlocked ? 1 : 0);
                    cmd.Parameters.AddWithValue("@IsClaudeUnlocked", node.IsClaudeUnlocked ? 1 : 0);
                    cmd.Parameters.AddWithValue("@IsGeminiUnlocked", node.IsGeminiUnlocked ? 1 : 0);
                    await cmd.ExecuteNonQueryAsync();
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
