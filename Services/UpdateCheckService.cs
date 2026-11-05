using System.Net.Http;
using System.Text.Json;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public static class UpdateCheckService
{
    public const string VersionCheckUrl = "https://www.chrelyonly.cn/version/chrelyonly-xray-version.json";
    public const long CurrentVersionCode = 20261105; // 当前客户端版本代号

    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public static async Task<(bool hasUpdate, UpdateInfo? info, string? errorMessage)> CheckForUpdateAsync()
    {
        try
        {
            AppLogger.LogSystem($"[版本更新] 正在检查最新版本信息 (当前版本代号: {CurrentVersionCode}, 检测地址: {VersionCheckUrl})...");

            using var response = await s_httpClient.GetAsync(VersionCheckUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, options);

            if (updateInfo != null)
            {
                bool hasUpdate = updateInfo.Version > CurrentVersionCode;
                AppLogger.LogSystem($"[版本更新] 版本检测完成: 远端版本={updateInfo.Version}, 是否有新版本={hasUpdate}, 标题={updateInfo.Title}");
                return (hasUpdate, updateInfo, null);
            }

            return (false, null, "未能成功解析远端版本 JSON 格式。");
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[版本更新] 版本检测异常: {ex.Message}");
            return (false, null, ex.Message);
        }
    }
}
