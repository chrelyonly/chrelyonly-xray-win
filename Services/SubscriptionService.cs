using System.Diagnostics;
using System.Net.Http;
using System.Text;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public static class SubscriptionService
{
    private static readonly HttpClient HttpClient;

    static SubscriptionService()
    {
        HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // 许多订阅服务器会拒绝默认的 .NET UA，必须携带合法的 User-Agent
        try
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "chrelyonly-xray-win/1.0 (v2rayN/Clash)");
        }
        catch { }
    }

    public static async Task<List<ServerNode>> FetchSubscriptionNodesAsync(string subscriptionUrl, bool useProxy = false)
    {
        var result = new List<ServerNode>();
        if (string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            Debug.WriteLine("[订阅解析] 错误：订阅地址为空。");
            return result;
        }

        try
        {
            Debug.WriteLine($"[订阅解析] 正在向订阅服务器发起 HTTP 请求: {subscriptionUrl} (代理模式: {useProxy})");
            HttpClient client = HttpClient;
            if (useProxy)
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new System.Net.WebProxy("127.0.0.1", 10809),
                    UseProxy = true
                };
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "chrelyonly-xray-win/1.0 (v2rayN/Clash)");
                }
                catch { }
            }

            var response = await client.GetAsync(subscriptionUrl);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[订阅解析] 错误：HTTP 请求失败，状态码: {(int)response.StatusCode} {response.ReasonPhrase}");
                throw new HttpRequestException($"服务器返回错误状态码: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[订阅解析] HTTP 请求成功，获取到原始内容长度: {content.Length} 字符");

            var decodedContent = TryDecodeBase64(content.Trim());
            if (decodedContent != content.Trim())
            {
                Debug.WriteLine("[订阅解析] 成功检测并解码 Base64 订阅内容。");
            }
            else
            {
                Debug.WriteLine("[订阅解析] 内容为明文（非 Base64 或解码跳过），按普通文本解析。");
            }

            var lines = decodedContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.WriteLine($"[订阅解析] 按行分割完成，总行数: {lines.Length}");

            int parsedCount = 0;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var node = ProxyLinkParser.ParseShareLink(trimmed);
                if (node != null)
                {
                    result.Add(node);
                    parsedCount++;
                }
                else
                {
                    Debug.WriteLine($"[订阅解析] 警告：无法解析的分享链接片段: {(trimmed.Length > 30 ? trimmed.Substring(0, 30) + "..." : trimmed)}");
                }
            }

            Debug.WriteLine($"[订阅解析] 解析完成！成功提取到 {parsedCount} 个有效节点。");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[订阅解析] 异常：抓取或解析订阅失败。原因: {ex.Message}");
            throw; // 向上抛出异常以便弹窗展示具体错误
        }

        return result;
    }

    private static string TryDecodeBase64(string input)
    {
        try
        {
            string padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            var bytes = Convert.FromBase64String(padded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return input;
        }
    }
}
