using System.Diagnostics;
using System.Text;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public static class YamlSubscriptionParser
{
    public static bool IsYamlContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        string trimmed = content.Trim();
        return trimmed.Contains("proxies:") || trimmed.Contains("proxy-groups:") || (trimmed.Contains("port:") && trimmed.Contains("server:"));
    }

    public static List<ServerNode> ParseYamlProxies(string content)
    {
        var result = new List<ServerNode>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        try
        {
            var rawEntries = ExtractProxyRawEntries(content);
            foreach (var rawEntry in rawEntries)
            {
                try
                {
                    var dict = ParseYamlDict(rawEntry);
                    var node = MapDictToServerNode(dict);
                    if (node != null && !string.IsNullOrWhiteSpace(node.Address))
                    {
                        result.Add(node);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[YAML解析] 解析单个节点异常: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[YAML解析] 解析 YAML 内容整体发生异常: {ex.Message}");
        }

        return result;
    }

    private static List<string> ExtractProxyRawEntries(string content)
    {
        var entries = new List<string>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        bool inProxiesSection = false;
        StringBuilder? currentEntrySb = null;

        foreach (var rawLine in lines)
        {
            string line = rawLine.TrimEnd();
            string trimmed = line.Trim();

            // Check section headers
            if (!inProxiesSection)
            {
                if (trimmed.StartsWith("proxies:", StringComparison.OrdinalIgnoreCase))
                {
                    inProxiesSection = true;
                }
                continue;
            }

            // If we are in proxies section and hit a new top-level key (no indentation, ends with colon)
            if (inProxiesSection && line.Length > 0 && !char.IsWhiteSpace(line[0]) && line.Contains(":") && !line.TrimStart().StartsWith("-"))
            {
                // Reached next section
                break;
            }

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

            // Check if a new item starts (starts with "- ")
            int dashIndex = line.IndexOf('-');
            if (dashIndex >= 0 && (dashIndex == 0 || line.Substring(0, dashIndex).Trim().Length == 0))
            {
                string afterDash = line.Substring(dashIndex + 1).Trim();
                if (afterDash.Length > 0)
                {
                    if (currentEntrySb != null)
                    {
                        entries.Add(currentEntrySb.ToString());
                    }
                    currentEntrySb = new StringBuilder();
                    currentEntrySb.AppendLine(afterDash);
                    continue;
                }
            }

            if (currentEntrySb != null)
            {
                currentEntrySb.AppendLine(line);
            }
        }

        if (currentEntrySb != null)
        {
            entries.Add(currentEntrySb.ToString());
        }

        return entries;
    }

    private static Dictionary<string, object> ParseYamlDict(string rawEntry)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        string trimmed = rawEntry.Trim();

        // Check if inline flow style: { key: val, ... }
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            ParseInlineFlowDict(trimmed, dict);
            return dict;
        }

        // Block style or hybrid
        var lines = rawEntry.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        string? currentChildKey = null;
        Dictionary<string, object>? currentChildDict = null;

        foreach (var line in lines)
        {
            string lineTrimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(lineTrimmed) || lineTrimmed.StartsWith("#")) continue;

            int colonIdx = lineTrimmed.IndexOf(':');
            if (colonIdx > 0)
            {
                string key = lineTrimmed.Substring(0, colonIdx).Trim();
                string val = lineTrimmed.Substring(colonIdx + 1).Trim();

                if (val.StartsWith("{") && val.EndsWith("}"))
                {
                    var childDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    ParseInlineFlowDict(val, childDict);
                    dict[key] = childDict;
                    currentChildKey = null;
                    continue;
                }

                int indent = line.TakeWhile(char.IsWhiteSpace).Count();
                if (indent > 2 && currentChildKey != null && currentChildDict != null)
                {
                    currentChildDict[key] = CleanValue(val);
                    continue;
                }

                if (string.IsNullOrEmpty(val))
                {
                    currentChildKey = key;
                    currentChildDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    dict[key] = currentChildDict;
                    continue;
                }

                dict[key] = CleanValue(val);
                currentChildKey = null;
            }
        }

        return dict;
    }

    private static void ParseInlineFlowDict(string text, Dictionary<string, object> dict)
    {
        string inner = text.Trim();
        if (inner.StartsWith("{")) inner = inner.Substring(1);
        if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1);
        inner = inner.Trim();

        int pos = 0;
        int len = inner.Length;

        while (pos < len)
        {
            while (pos < len && (char.IsWhiteSpace(inner[pos]) || inner[pos] == ',')) pos++;
            if (pos >= len) break;

            int keyStart = pos;
            while (pos < len && inner[pos] != ':') pos++;
            if (pos >= len) break;

            string key = inner.Substring(keyStart, pos - keyStart).Trim();
            pos++; // skip ':'

            while (pos < len && char.IsWhiteSpace(inner[pos])) pos++;
            if (pos >= len) break;

            if (inner[pos] == '{')
            {
                int braceDepth = 0;
                int valStart = pos;
                while (pos < len)
                {
                    if (inner[pos] == '{') braceDepth++;
                    else if (inner[pos] == '}')
                    {
                        braceDepth--;
                        if (braceDepth == 0) { pos++; break; }
                    }
                    pos++;
                }
                string nestedDictStr = inner.Substring(valStart, pos - valStart);
                var childDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                ParseInlineFlowDict(nestedDictStr, childDict);
                dict[key] = childDict;
            }
            else if (inner[pos] == '[')
            {
                int bracketDepth = 0;
                int valStart = pos;
                while (pos < len)
                {
                    if (inner[pos] == '[') bracketDepth++;
                    else if (inner[pos] == ']')
                    {
                        bracketDepth--;
                        if (bracketDepth == 0) { pos++; break; }
                    }
                    pos++;
                }
                string nestedListStr = inner.Substring(valStart, pos - valStart);
                dict[key] = nestedListStr;
            }
            else
            {
                int valStart = pos;
                int braceCount = 0;
                int bracketCount = 0;
                bool inQuotes = false;
                char quoteChar = '\0';

                while (pos < len)
                {
                    char c = inner[pos];
                    if ((c == '"' || c == '\'') && !inQuotes)
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (inQuotes && c == quoteChar)
                    {
                        inQuotes = false;
                    }
                    else if (!inQuotes)
                    {
                        if (c == '{') braceCount++;
                        else if (c == '}') braceCount--;
                        else if (c == '[') bracketCount++;
                        else if (c == ']') bracketCount--;
                        else if (c == ',' && braceCount == 0 && bracketCount == 0)
                        {
                            break;
                        }
                    }
                    pos++;
                }
                string valStr = inner.Substring(valStart, pos - valStart).Trim();
                dict[key] = CleanValue(valStr);
            }
        }
    }

    private static string CleanValue(string val)
    {
        val = val.Trim();
        if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
        {
            if (val.Length >= 2)
            {
                val = val.Substring(1, val.Length - 2);
            }
        }
        return val;
    }

    private static ServerNode? MapDictToServerNode(Dictionary<string, object> dict)
    {
        if (dict.Count == 0) return null;

        string GetString(string key, string defaultValue = "")
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                return val.ToString()?.Trim() ?? defaultValue;
            }
            return defaultValue;
        }

        int GetInt(string key, int defaultValue = 0)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                if (int.TryParse(val.ToString(), out int parsed)) return parsed;
            }
            return defaultValue;
        }

        bool GetBool(string key, bool defaultValue = false)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                string s = val.ToString()?.Trim().ToLowerInvariant() ?? "";
                if (s == "true" || s == "1") return true;
                if (s == "false" || s == "0") return false;
            }
            return defaultValue;
        }

        Dictionary<string, object>? GetSubDict(string key)
        {
            if (dict.TryGetValue(key, out var val) && val is Dictionary<string, object> sub)
            {
                return sub;
            }
            return null;
        }

        string type = GetString("type").ToLowerInvariant();
        string server = GetString("server");
        int port = GetInt("port");
        string name = GetString("name");

        if (string.IsNullOrWhiteSpace(server) || port <= 0) return null;

        var node = new ServerNode
        {
            Name = string.IsNullOrWhiteSpace(name) ? server : name,
            Address = server,
            Port = port
        };

        if (type == "vless")
        {
            node.Protocol = "VLESS";
            node.Uuid = GetString("uuid");
            node.Flow = GetString("flow");
            node.Encryption = GetString("encryption", "none");
            if (string.IsNullOrWhiteSpace(node.Encryption)) node.Encryption = "none";

            bool isTls = GetBool("tls");
            string servername = GetString("servername", GetString("sni"));
            node.Sni = servername;

            // Reality options
            var realityOpts = GetSubDict("reality-opts");
            if (realityOpts != null)
            {
                node.Security = "Reality";
                if (realityOpts.TryGetValue("public-key", out var pbk)) node.PublicKey = pbk?.ToString() ?? "";
                if (realityOpts.TryGetValue("short-id", out var sid)) node.ShortId = sid?.ToString() ?? "";
                if (realityOpts.TryGetValue("spider-x", out var spx)) node.Spx = spx?.ToString() ?? "";
            }
            else if (isTls)
            {
                node.Security = "TLS";
            }

            string network = GetString("network", "tcp").ToLowerInvariant();
            node.Transport = network == "ws" ? "ws" : (network == "grpc" ? "grpc" : (network == "xhttp" ? "xhttp" : "tcp"));

            var wsOpts = GetSubDict("ws-opts");
            if (wsOpts != null)
            {
                if (wsOpts.TryGetValue("path", out var path)) node.Path = path?.ToString() ?? "";
                if (wsOpts.TryGetValue("headers", out var headersObj))
                {
                    if (headersObj is Dictionary<string, object> headersDict)
                    {
                        if (headersDict.TryGetValue("Host", out var hostVal) || headersDict.TryGetValue("host", out hostVal))
                        {
                            node.Host = hostVal?.ToString() ?? "";
                        }
                    }
                }
            }
            var grpcOpts = GetSubDict("grpc-opts");
            if (grpcOpts != null)
            {
                if (grpcOpts.TryGetValue("grpc-service-name", out var gName) || grpcOpts.TryGetValue("serviceName", out gName))
                {
                    node.Path = gName?.ToString() ?? "";
                }
            }
        }
        else if (type == "hysteria2" || type == "hy2" || type == "hysteria")
        {
            node.Protocol = "hysteria";
            node.Uuid = GetString("password", GetString("auth"));
            node.Sni = GetString("sni", GetString("servername"));
            node.Obfs = GetString("obfs");
            node.ObfsPassword = GetString("obfs-password");
        }
        else if (type == "trojan")
        {
            node.Protocol = "Trojan";
            node.Uuid = GetString("password");
            node.Security = "TLS";
            node.Sni = GetString("sni", GetString("servername"));
            string network = GetString("network", "tcp").ToLowerInvariant();
            node.Transport = network;
            var wsOpts = GetSubDict("ws-opts");
            if (wsOpts != null)
            {
                if (wsOpts.TryGetValue("path", out var path)) node.Path = path?.ToString() ?? "";
            }
        }
        else if (type == "vmess")
        {
            node.Protocol = "VMess";
            node.Uuid = GetString("uuid");
            node.Security = GetBool("tls") ? "TLS" : "none";
            node.Transport = GetString("network", "tcp");
            var wsOpts = GetSubDict("ws-opts");
            if (wsOpts != null)
            {
                if (wsOpts.TryGetValue("path", out var path)) node.Path = path?.ToString() ?? "";
            }
        }
        else if (type == "ss" || type == "shadowsocks")
        {
            node.Protocol = "Shadowsocks";
            node.Uuid = GetString("password");
            node.Encryption = GetString("cipher", GetString("encryption"));
            node.Obfs = GetString("plugin");
            node.ObfsPassword = GetString("plugin-opts");
        }
        else
        {
            node.Protocol = type.ToUpperInvariant();
            node.Uuid = GetString("uuid", GetString("password"));
        }

        return node;
    }
}
