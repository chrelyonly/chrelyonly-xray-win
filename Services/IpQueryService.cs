using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace chrelyonly_xray_win.Services;

public class IpInfo
{
    public string Ip { get; set; } = "未知";
    public string Country { get; set; } = "";
    public string Region { get; set; } = "";
    public string City { get; set; } = "";
    public string Isp { get; set; } = "";

    public string LocationSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Country) && string.IsNullOrWhiteSpace(Isp))
                return "未知位置";

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country);
            if (!string.IsNullOrWhiteSpace(Region) && Region != Country) parts.Add(Region);
            if (!string.IsNullOrWhiteSpace(City) && City != Region) parts.Add(City);
            if (!string.IsNullOrWhiteSpace(Isp)) parts.Add(Isp);

            return string.Join(" ", parts);
        }
    }
}

public static class IpQueryService
{
    /// <summary>
    /// 查询本地直连公网 IP (不经过代理)
    /// </summary>
    public static async Task<IpInfo> GetLocalPublicIpAsync()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        return await FetchIpInfoAsync(client);
    }

    /// <summary>
    /// 查询出口 IP (通过本地 HTTP 代理 127.0.0.1:10909)
    /// </summary>
    public static async Task<IpInfo> GetOutboundIpAsync(string proxyAddress = "http://127.0.0.1:10909")
    {
        var handler = new HttpClientHandler
        {
            Proxy = new System.Net.WebProxy(proxyAddress),
            UseProxy = true,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        return await FetchIpInfoAsync(client);
    }

    private static async Task<IpInfo> FetchIpInfoAsync(HttpClient client)
    {
        // 尝试从 ip-api.com 获取
        try
        {
            string json = await client.GetStringAsync("http://ip-api.com/json/?lang=zh-CN");
            var node = JsonNode.Parse(json);
            if (node != null && node["status"]?.ToString() == "success")
            {
                return new IpInfo
                {
                    Ip = node["query"]?.ToString() ?? "未知",
                    Country = node["country"]?.ToString() ?? "",
                    Region = node["regionName"]?.ToString() ?? "",
                    City = node["city"]?.ToString() ?? "",
                    Isp = node["isp"]?.ToString() ?? ""
                };
            }
        }
        catch { }

        // 备用接口 1: ip.sb
        try
        {
            string json = await client.GetStringAsync("https://api.ip.sb/geoip");
            var node = JsonNode.Parse(json);
            if (node != null)
            {
                return new IpInfo
                {
                    Ip = node["ip"]?.ToString() ?? "未知",
                    Country = node["country"]?.ToString() ?? "",
                    Region = node["region"]?.ToString() ?? "",
                    City = node["city"]?.ToString() ?? "",
                    Isp = node["organization"]?.ToString() ?? node["isp"]?.ToString() ?? ""
                };
            }
        }
        catch { }

        // 备用接口 2: ipify.org (仅返回纯 IP)
        try
        {
            string ipStr = (await client.GetStringAsync("https://api.ipify.org")).Trim();
            if (!string.IsNullOrWhiteSpace(ipStr))
            {
                return new IpInfo { Ip = ipStr };
            }
        }
        catch { }

        return new IpInfo { Ip = "获取失败" };
    }
}
