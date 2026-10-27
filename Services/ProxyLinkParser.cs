using System.Web;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public static class ProxyLinkParser
{
    public static ServerNode? ParseShareLink(string shareUrl)
    {
        if (string.IsNullOrWhiteSpace(shareUrl)) return null;

        try
        {
            shareUrl = shareUrl.Trim();
            if (shareUrl.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
            {
                return ParseVless(shareUrl);
            }
            if (shareUrl.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase))
            {
                return ParseHy2(shareUrl);
            }
        }
        catch
        {
            // fallback or ignore parse error
        }

        return null;
    }

    private static ServerNode ParseVless(string url)
    {
        // Format: vless://uuid@host:port?security=...&sni=...&type=...#remark
        var uri = new Uri(url);
        var userInfo = HttpUtility.UrlDecode(uri.UserInfo);
        var remark = HttpUtility.UrlDecode(uri.Fragment.TrimStart('#'));
        if (string.IsNullOrEmpty(remark)) remark = uri.Host;

        var query = HttpUtility.ParseQueryString(uri.Query);

        return new ServerNode
        {
            Name = remark,
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 443,
            Protocol = "VLESS",
            Uuid = userInfo,
            Security = query["security"] ?? "Reality",
            Transport = query["type"]?.ToUpperInvariant() ?? "TCP",
            Sni = query["sni"] ?? "",
            PublicKey = query["pbk"] ?? "",
            ShortId = query["sid"] ?? "",
            Flow = query["flow"] ?? "",
            Encryption = query["encryption"] ?? "",
            Ech = query["ech"] ?? "",
            Host = query["host"] ?? "",
            Path = query["path"] ?? "",
            Email = query["Email"] ?? "t@t.tt",
        };
    }
    private static ServerNode ParseHy2(string url)
    {
        // Format: vless://uuid@host:port?security=...&sni=...&type=...#remark
        var uri = new Uri(url);
        var userInfo = HttpUtility.UrlDecode(uri.UserInfo);
        var remark = HttpUtility.UrlDecode(uri.Fragment.TrimStart('#'));
        if (string.IsNullOrEmpty(remark)) remark = uri.Host;

        var query = HttpUtility.ParseQueryString(uri.Query);

        return new ServerNode
        {
            Name = remark,
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 443,
            Protocol = "VLESS",
            Uuid = userInfo,
            Sni = query["sni"] ?? "",
            Obfs = query["obfs"] ?? "",
            ObfsPassword = query["obfs-password"] ?? "",
        };
    }

    private static ServerNode ParseVmess(string url)
    {
        // Simplified VMess parse placeholder
        var node = new ServerNode
        {
            Name = "VMess 节点",
            Address = "vmess.example.com",
            Port = 443,
            Protocol = "VMess",
            Security = "auto",
            Transport = "TCP"
        };
        return node;
    }

    private static ServerNode ParseTrojan(string url)
    {
        var uri = new Uri(url);
        var remark = HttpUtility.UrlDecode(uri.Fragment.TrimStart('#'));
        if (string.IsNullOrEmpty(remark)) remark = uri.Host;

        return new ServerNode
        {
            Name = remark,
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 443,
            Protocol = "Trojan",
            Security = "TLS",
            Transport = "TCP"
        };
    }
}
