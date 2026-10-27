using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public class XrayCoreManager
{
    private Process? _xrayProcess;

    public string CoreFolderPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core");
    public string XrayExePath => Path.Combine(CoreFolderPath, "xraylib.exe");
    public string ConfigPath => Path.Combine(CoreFolderPath, "config.json");
    public string LogFilePath => Path.Combine(CoreFolderPath, "xray.log");

    public bool IsRunning => _xrayProcess != null && !_xrayProcess.HasExited;


    public string GenerateClientConfig(
    ServerNode node,
    bool allowLan = false,
    bool isGlobalMode = false)
{
    try
    {
        string listenAddress = allowLan ? "0.0.0.0" : "127.0.0.1";
        string modeText = isGlobalMode ? "全局代理" : "智能分流";

        AppLogger.LogSystem(
            $"[XRAY核心] 正在为节点「{node.Name}」" +
            $"({node.Address}:{node.Port}, 协议: {node.Protocol}, " +
            $"监听: {listenAddress}, 模式: {modeText}) 生成客户端配置...");

        var root = new JsonObject
        {
            ["log"] = BuildLog(),
            ["dns"] = BuildDns(),
            ["inbounds"] = BuildInbounds(listenAddress),
            ["outbounds"] = BuildOutbounds(node),
            ["routing"] = BuildRouting(isGlobalMode)
        };

        string configJson = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });

        AppLogger.LogSystem(
            $"[XRAY核心] 客户端配置文件生成成功，路径: {ConfigPath}");

        return configJson;
    }
    catch (Exception ex)
    {
        AppLogger.LogSystem(
            $"[XRAY核心] 客户端配置生成失败: {ex.Message}");

        throw;
    }
}

/// <summary>
/// 构建日志配置
/// </summary>
private JsonObject BuildLog()
{
    return new JsonObject
    {
        ["loglevel"] = "warning"
    };
}
/// <summary>
/// 构建dns配置
/// </summary>
private JsonObject BuildDns()
{
    
    string json = """
      {
        "hosts": {
          "dns.google": [
            "8.8.8.8",
            "8.8.4.4",
            "2001:4860:4860::8888",
            "2001:4860:4860::8844"
          ],
          "dns.alidns.com": [
            "223.5.5.5",
            "223.6.6.6",
            "2400:3200::1",
            "2400:3200:baba::1"
          ],
          "one.one.one.one": [
            "1.1.1.1",
            "1.0.0.1",
            "2606:4700:4700::1111",
            "2606:4700:4700::1001"
          ],
          "1dot1dot1dot1.cloudflare-dns.com": [
            "1.1.1.1",
            "1.0.0.1",
            "2606:4700:4700::1111",
            "2606:4700:4700::1001"
          ],
          "cloudflare-dns.com": [
            "104.16.249.249",
            "104.16.248.249",
            "2606:4700::6810:f8f9",
            "2606:4700::6810:f9f9"
          ],
          "dns.cloudflare.com": [
            "162.159.61.8",
            "172.64.41.8",
            "2a06:98c1:52::8",
            "2803:f800:53::8"
          ],
          "dot.pub": [
            "1.12.12.12",
            "120.53.53.53"
          ],
          "doh.pub": [
            "1.12.12.12",
            "120.53.53.53"
          ],
          "dns.quad9.net": [
            "9.9.9.9",
            "149.112.112.112",
            "2620:fe::fe",
            "2620:fe::9"
          ],
          "dns.yandex.net": [
            "77.88.8.8",
            "77.88.8.1",
            "2a02:6b8::feed:0ff",
            "2a02:6b8:0:1::feed:0ff"
          ],
          "dns.sb": [
            "45.11.45.11",
            "185.222.222.222",
            "2a09::",
            "2a11::"
          ],
          "dns.umbrella.com": [
            "208.67.220.220",
            "208.67.222.222",
            "2620:119:35::35",
            "2620:119:53::53"
          ],
          "dns.sse.cisco.com": [
            "208.67.220.220",
            "208.67.222.222",
            "2620:119:35::35",
            "2620:119:53::53"
          ],
          "engage.cloudflareclient.com": [
            "162.159.192.1",
            "2606:4700:d0::a29f:c001"
          ]
        },
        "servers": [
          {
            "address": "223.5.5.5",
            "domains": [
              "cf.877774.xyz",
              "cloudflare-ech.com"
            ],
            "skipFallback": true,
            "tag": "direct-dns-1"
          },
          {
            "address": "223.5.5.5",
            "domains": [
              "domain:alidns.com",
              "domain:doh.pub",
              "domain:dot.pub",
              "domain:360.cn",
              "domain:onedns.net"
            ],
            "skipFallback": true,
            "tag": "direct-dns-2"
          },
          {
            "address": "1.1.1.1",
            "domains": [
              "geosite:google"
            ],
            "skipFallback": true
          },
          {
            "address": "223.5.5.5",
            "domains": [
              "geosite:private",
              "geosite:cn"
            ],
            "skipFallback": true,
            "tag": "direct-dns-3"
          },
          "1.1.1.1"
        ],
        "tag": "dns-module"
      }
      """;
    return JsonObject.Parse(json)!.AsObject();
}

/// <summary>
/// 构建入站配置
/// </summary>
private JsonArray BuildInbounds(string listenAddress)
{
    return JsonNode.Parse($$"""
                          [
                              {
                             "tag": "socks",
                             "port": 10809,
                             "listen": "{{listenAddress}}",
                             "protocol": "mixed",
                             "sniffing": {
                               "enabled": true,
                               "destOverride": [
                                 "http",
                                 "tls",
                                 "quic",
                                 "fakedns"
                               ],
                               "routeOnly": true
                             },
                             "settings": {
                               "auth": "noauth",
                               "udp": true,
                               "allowTransparent": false
                             }
                           }
                          ]
                          """)!.AsArray();
}

/// <summary>
/// 构建出站配置
/// </summary>
private JsonArray BuildOutbounds(ServerNode node)
{
    var outbounds = new JsonArray();

    string address = node.Address;
    int port = node.Port;
    string email = node.Email;
    string encryption = node.Encryption;
    string transport = node.Transport;
    string security = node.Security;

    // 代理出站
    var proxyOutbound = new JsonObject
    {
        ["tag"] = "proxy",
        ["protocol"] = node.Protocol,
        ["settings"] = new JsonObject
        {
            ["address"] = address,
            ["port"] = port,
            ["email"] = email,
            ["id"] = node.Uuid,
            ["encryption"] = encryption,
        },
        ["streamSettings"] = new JsonObject
        {
            ["network"] = transport,
            ["security"] = security,
        }
    };


    // 如果是tls加密
    if (security.Equals("tls", StringComparison.OrdinalIgnoreCase))
    {
        proxyOutbound["streamSettings"]["tlsSettings"] = new JsonObject()
        {
            ["serverName"] = node.Host,
            ["alpn"] = new JsonArray()
            {
                "h2",
                "http/1.1",
                "h3"
            },
            ["fingerprint"] =  "chrome",
            ["echConfigList"] = node.Ech,
            ["echForceQuery"] = "full"
        };
    }
    
    // 如果是ws传输方式
    if (transport.Equals("ws", StringComparison.OrdinalIgnoreCase))
    {
        proxyOutbound["streamSettings"]["wsSettings"] = new JsonObject()
        {

            ["path"] = node.Path,
            ["host"] = node.Host,
            ["headers"] =  new JsonObject()
            {
                ["User-Agent"] = "chrome"
            }
        };
    }
    
    
    
    outbounds.Add(proxyOutbound);

    // 直连出站
    outbounds.Add(new JsonObject
    {
        ["tag"] = "direct",
        ["protocol"] = "freedom",
    });

    // 屏蔽出站
    outbounds.Add(new JsonObject
    {
        ["tag"] = "block",
        ["protocol"] = "blackhole",
    });

    return outbounds;
}

/// <summary>
/// 构建路由配置
/// </summary>
private JsonObject BuildRouting(bool isGlobalMode)
{
    string json = """
      {
        "domainStrategy": "AsIs",
        "rules": [
          {
            "type": "field",
            "port": "443",
            "network": "udp",
            "outboundTag": "block"
          },
          {
            "type": "field",
            "outboundTag": "proxy",
            "domain": [
              "geosite:google"
            ]
          },
          {
            "type": "field",
            "outboundTag": "direct",
            "ip": [
              "geoip:private"
            ]
          },
          {
            "type": "field",
            "outboundTag": "direct",
            "domain": [
              "geosite:private"
            ]
          },
          {
            "type": "field",
            "outboundTag": "direct",
            "ip": [
              "223.5.5.5",
              "223.6.6.6",
              "2400:3200::1",
              "2400:3200:baba::1",
              "119.29.29.29",
              "1.12.12.12",
              "120.53.53.53",
              "2402:4e00::",
              "2402:4e00:1::",
              "180.76.76.76",
              "2400:da00::6666",
              "114.114.114.114",
              "114.114.115.115",
              "114.114.114.119",
              "114.114.115.119",
              "114.114.114.110",
              "114.114.115.110",
              "180.184.1.1",
              "180.184.2.2",
              "101.226.4.6",
              "218.30.118.6",
              "123.125.81.6",
              "140.207.198.6",
              "1.2.4.8",
              "210.2.4.8",
              "52.80.66.66",
              "117.50.22.22",
              "2400:7fc0:849e:200::4",
              "2404:c2c0:85d8:901::4",
              "117.50.10.10",
              "52.80.52.52",
              "2400:7fc0:849e:200::8",
              "2404:c2c0:85d8:901::8",
              "117.50.60.30",
              "52.80.60.30"
            ]
          },
          {
            "type": "field",
            "outboundTag": "direct",
            "domain": [
              "domain:alidns.com",
              "domain:doh.pub",
              "domain:dot.pub",
              "domain:360.cn",
              "domain:onedns.net"
            ]
          },
          {
            "type": "field",
            "outboundTag": "direct",
            "ip": [
              "geoip:cn"
            ]
          },
          {
            "type": "field",
            "outboundTag": "direct",
            "domain": [
              "geosite:cn"
            ]
          },
          {
            "type": "field",
            "inboundTag": [
              "direct-dns-1",
              "direct-dns-2",
              "direct-dns-3"
            ],
            "outboundTag": "direct"
          },
          {
            "type": "field",
            "inboundTag": [
              "dns-module"
            ],
            "outboundTag": "proxy"
          }
        ]
      }
      """;
    return JsonObject.Parse(json)!.AsObject();
}










    public bool StartCore(string configJson)
    {
        try
        {
            AppLogger.LogSystem("[XRAY核心] 准备启动 Xray 内核服务...");
            if (!Directory.Exists(CoreFolderPath))
            {
                Directory.CreateDirectory(CoreFolderPath);
                AppLogger.LogSystem($"[XRAY核心] 创建 Core 目录: {CoreFolderPath}");
            }

            if (!File.Exists(XrayExePath))
            {
                AppLogger.LogSystem($"[XRAY核心] 错误：未在指定路径找到 xraylib.exe，路径: {XrayExePath}");
                return false;
            }

            File.WriteAllText(ConfigPath, configJson);
            AppLogger.LogSystem("[XRAY核心] config.json 写入成功。");

            StopCore();

            var startInfo = new ProcessStartInfo
            {
                FileName = XrayExePath,
                Arguments = "run -c config.json",
                WorkingDirectory = CoreFolderPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _xrayProcess = Process.Start(startInfo);
            if (_xrayProcess != null)
            {
                AppLogger.LogSystem($"[XRAY核心] Xray 进程已成功启动，PID: {_xrayProcess.Id}");

                _xrayProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string logLine = $"[OUT] {e.Data}";
                        AppLogger.LogCore(logLine);
                        AppendLogFile(logLine);
                    }
                };

                _xrayProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string logLine = $"[ERR] {e.Data}";
                        AppLogger.LogCore(logLine);
                        AppendLogFile(logLine);
                    }
                };

                _xrayProcess.BeginOutputReadLine();
                _xrayProcess.BeginErrorReadLine();
                return true;
            }
            AppLogger.LogSystem("[XRAY核心] 错误：Process.Start 返回 null，未能启动内核进程。");
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[XRAY核心] 启动核心发生异常: {ex.Message}");
            return false;
        }
    }

    public void StopCore()
    {
        try
        {
            if (_xrayProcess != null)
            {
                AppLogger.LogSystem("[XRAY核心] 正在停止 Xray 内核进程及子进程树...");
                if (!_xrayProcess.HasExited)
                {
                    _xrayProcess.Kill(true);
                }
                _xrayProcess.Dispose();
                _xrayProcess = null;
                AppLogger.LogSystem("[XRAY核心] Xray 内核进程已安全停止。");
            }
            else
            {
                AppLogger.LogSystem("[XRAY核心] 停止忽略：当前没有运行中的 Xray 进程。");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[XRAY核心] 停止核心发生异常: {ex.Message}");
        }
    }

    private void AppendLogFile(string line)
    {
        try
        {
            lock (this)
            {
                if (File.Exists(LogFilePath))
                {
                    var fi = new FileInfo(LogFilePath);
                    if (fi.Length > 5 * 1024 * 1024) // 5 MB size limit
                    {
                        File.WriteAllText(LogFilePath, "");
                    }
                }
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss}] {line}" + Environment.NewLine);
            }
        }
        catch { }
    }
}
