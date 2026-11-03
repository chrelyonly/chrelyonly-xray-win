using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
            if (shareUrl.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase) || shareUrl.StartsWith("hysteria://", StringComparison.OrdinalIgnoreCase))
            {
                return ParseHy2(shareUrl);
            }
            if (shareUrl.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase))
            {
                return ParseTrojan(shareUrl);
            }
            if (shareUrl.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
            {
                return ParseVmess(shareUrl);
            }
        }
        catch
        {
            // fallback or ignore parse error
        }

        return null;
    }

    public static string ExportShareLink(ServerNode node)
    {
        if (node == null) return string.Empty;
        try
        {
            var protocol = node.Protocol?.ToLowerInvariant() ?? "vless";
            var nameEnc = Uri.EscapeDataString(node.Name ?? "Node");

            if (protocol.Contains("vless"))
            {
                var sb = new StringBuilder();
                sb.Append($"vless://{node.Uuid}@{node.Address}:{node.Port}?");
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(node.Security)) queryParams.Add($"security={node.Security}");
                if (!string.IsNullOrWhiteSpace(node.Transport)) queryParams.Add($"type={node.Transport.ToLowerInvariant()}");
                if (!string.IsNullOrWhiteSpace(node.Sni)) queryParams.Add($"sni={Uri.EscapeDataString(node.Sni)}");
                if (!string.IsNullOrWhiteSpace(node.PublicKey)) queryParams.Add($"pbk={Uri.EscapeDataString(node.PublicKey)}");
                if (!string.IsNullOrWhiteSpace(node.ShortId)) queryParams.Add($"sid={Uri.EscapeDataString(node.ShortId)}");
                if (!string.IsNullOrWhiteSpace(node.Spx)) queryParams.Add($"spx={Uri.EscapeDataString(node.Spx)}");
                if (!string.IsNullOrWhiteSpace(node.Flow)) queryParams.Add($"flow={Uri.EscapeDataString(node.Flow)}");
                if (!string.IsNullOrWhiteSpace(node.Encryption)) queryParams.Add($"encryption={Uri.EscapeDataString(node.Encryption)}");
                if (!string.IsNullOrWhiteSpace(node.Host)) queryParams.Add($"host={Uri.EscapeDataString(node.Host)}");
                if (!string.IsNullOrWhiteSpace(node.Path)) queryParams.Add($"path={Uri.EscapeDataString(node.Path)}");

                sb.Append(string.Join("&", queryParams));
                sb.Append($"#{nameEnc}");
                return sb.ToString();
            }
            else if (protocol.Contains("hysteria") || protocol.Contains("hy2"))
            {
                var sb = new StringBuilder();
                sb.Append($"hysteria2://{node.Uuid}@{node.Address}:{node.Port}?");
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(node.Sni)) queryParams.Add($"sni={Uri.EscapeDataString(node.Sni)}");
                if (!string.IsNullOrWhiteSpace(node.Obfs)) queryParams.Add($"obfs={Uri.EscapeDataString(node.Obfs)}");
                if (!string.IsNullOrWhiteSpace(node.ObfsPassword)) queryParams.Add($"obfs-password={Uri.EscapeDataString(node.ObfsPassword)}");
                sb.Append(string.Join("&", queryParams));
                sb.Append($"#{nameEnc}");
                return sb.ToString();
            }
            else if (protocol.Contains("trojan"))
            {
                var sb = new StringBuilder();
                sb.Append($"trojan://{node.Uuid}@{node.Address}:{node.Port}?");
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(node.Sni)) queryParams.Add($"sni={Uri.EscapeDataString(node.Sni)}");
                if (!string.IsNullOrWhiteSpace(node.Host)) queryParams.Add($"host={Uri.EscapeDataString(node.Host)}");
                if (!string.IsNullOrWhiteSpace(node.Path)) queryParams.Add($"path={Uri.EscapeDataString(node.Path)}");
                sb.Append(string.Join("&", queryParams));
                sb.Append($"#{nameEnc}");
                return sb.ToString();
            }
            else
            {
                return $"vless://{node.Uuid}@{node.Address}:{node.Port}?security=none#{nameEnc}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[导出链接] 发生异常: {ex.Message}");
            return $"vless://{node.Uuid}@{node.Address}:{node.Port}#{Uri.EscapeDataString(node.Name ?? "Node")}";
        }
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
            Spx = query["spx"] ?? "",
            Flow = query["flow"] ?? "",
            Encryption = string.IsNullOrWhiteSpace(query["encryption"]) ? "none" : query["encryption"],
            Ech = query["ech"] ?? "",
            Host = query["host"] ?? "",
            Path = query["path"] ?? "",
            Email = query["Email"] ?? "t@t.tt",
        };
    }
    private static ServerNode ParseHy2(string url)
    {
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
            Protocol = "hysteria",
            Uuid = userInfo,
            Sni = query["sni"] ?? "",
            Obfs = query["obfs"] ?? "",
            ObfsPassword = query["obfs-password"] ?? "",
        };
    }

    private static ServerNode ParseVmess(string url)
    {
        try
        {
            var base64Part = url.Substring("vmess://".Length).Trim();
            string decoded = TryDecodeBase64(base64Part);
            var node = new ServerNode
            {
                Protocol = "VMess",
                Name = "VMess 节点",
                Address = "127.0.0.1",
                Port = 443
            };
            using var doc = JsonDocument.Parse(decoded);
            var root = doc.RootElement;
            if (root.TryGetProperty("add", out var add)) node.Address = add.GetString() ?? "127.0.0.1";
            if (root.TryGetProperty("port", out var port))
            {
                if (port.ValueKind == JsonValueKind.Number) node.Port = port.GetInt32();
                else if (int.TryParse(port.GetString(), out var p)) node.Port = p;
            }
            if (root.TryGetProperty("ps", out var ps)) node.Name = ps.GetString() ?? "VMess 节点";
            if (root.TryGetProperty("id", out var id)) node.Uuid = id.GetString() ?? "";
            if (root.TryGetProperty("net", out var net)) node.Transport = net.GetString()?.ToUpperInvariant() ?? "TCP";
            if (root.TryGetProperty("tls", out var tls)) node.Security = (tls.GetString() == "tls") ? "TLS" : "none";
            if (root.TryGetProperty("host", out var host)) node.Host = host.GetString() ?? "";
            if (root.TryGetProperty("path", out var path)) node.Path = path.GetString() ?? "";
            return node;
        }
        catch
        {
            return new ServerNode
            {
                Name = "VMess 节点",
                Address = "vmess.example.com",
                Port = 443,
                Protocol = "VMess",
                Security = "auto",
                Transport = "TCP"
            };
        }
    }

    private static ServerNode ParseTrojan(string url)
    {
        var uri = new Uri(url);
        var remark = HttpUtility.UrlDecode(uri.Fragment.TrimStart('#'));
        if (string.IsNullOrEmpty(remark)) remark = uri.Host;

        var query = HttpUtility.ParseQueryString(uri.Query);

        return new ServerNode
        {
            Name = remark,
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 443,
            Protocol = "Trojan",
            Security = "TLS",
            Transport = "TCP",
            Uuid = uri.UserInfo ?? "",
            Sni = query["sni"] ?? "",
            Host = query["host"] ?? "",
            Path = query["path"] ?? ""
        };
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
                case 1: padded += "="; break;
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
