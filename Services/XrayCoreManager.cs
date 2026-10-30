using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

internal static class ChildProcessTracker
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        JOBOBJECTINFOCLASS JobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    private static readonly IntPtr s_jobHandle;

    static ChildProcessTracker()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        s_jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (s_jobHandle == IntPtr.Zero)
            return;

        var basicInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        };

        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = basicInfo
        };

        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
            SetInformationJobObject(
                s_jobHandle,
                JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                extendedInfoPtr,
                (uint)length);
        }
        catch
        {
            // Ignored
        }
        finally
        {
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }

    public static void AddProcess(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && s_jobHandle != IntPtr.Zero && process != null && !process.HasExited)
        {
            try
            {
                AssignProcessToJobObject(s_jobHandle, process.Handle);
            }
            catch
            {
                // Ignored
            }
        }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    private enum JOBOBJECTINFOCLASS
    {
        JobObjectExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }
}

public class XrayCoreManager
{
    private Process? _xrayProcess;

    public string CoreFolderPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core");
    public string XrayExePath => Path.Combine(CoreFolderPath, "xraylib.exe");
    public string ConfigPath => Path.Combine(CoreFolderPath, "config.json");
    public string LogFilePath => Path.Combine(CoreFolderPath, "xray.log");

    public bool IsRunning => _xrayProcess != null && !_xrayProcess.HasExited;

    public XrayCoreManager()
    {
        KillAllXrayProcesses();
    }

    public static void KillAllXrayProcesses()
    {
        try
        {
            string[] targetNames = new[] { "xraylib" };
            int currentPid = Environment.ProcessId;

            foreach (var name in targetNames)
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var proc in processes)
                {
                    try
                    {
                        if (proc.Id != currentPid && !proc.HasExited)
                        {
                            AppLogger.LogSystem($"[XRAY核心] 正在清理残留的 {name} 进程 (PID: {proc.Id})...");
                            proc.Kill(true);
                            proc.WaitForExit(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogSystem($"[XRAY核心] 清理进程 PID {proc.Id} 失败: {ex.Message}");
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[XRAY核心] 扫描并清理残留 Xray 进程异常: {ex.Message}");
        }
    }


    public string GenerateClientConfig(
        ServerNode node,
        bool allowLan = false,
        bool isGlobalMode = false,
        string customProxyRules = "",
        string customDirectRules = "")
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
                ["routing"] = BuildRouting(isGlobalMode, customProxyRules, customDirectRules)
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
                             "port": 10909,
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
    string email = string.IsNullOrWhiteSpace(node.Email) ? "t@t.tt" : node.Email;
    string encryption = string.IsNullOrWhiteSpace(node.Encryption) ? "none" : node.Encryption;
    string transport = node.Transport;
    string security = node.Security;

    // 代理出站
    var proxyOutbound = new JsonObject
    {
        ["tag"] = "proxy",
        ["protocol"] = node.Protocol,
        ["streamSettings"] = new JsonObject
        {
            ["network"] = transport,
            ["security"] = security,
        }
    };

    
    // 如果是协议是hy2
    if (node.Protocol.Equals("hysteria", StringComparison.OrdinalIgnoreCase))
    {
        proxyOutbound["settings"] = new JsonObject
        {
            ["address"] = address,
            ["port"] = port,
            ["version"] = 2,
        };
    }
    // 如果是vless
    if (node.Protocol.Equals("vless", StringComparison.OrdinalIgnoreCase))
    {
        proxyOutbound["settings"] = new JsonObject
        {
            ["vnext"] = new JsonArray
            {
                new JsonObject
                {
                    ["address"] = address,
                    ["port"] = port,
                    ["users"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = node.Uuid,
                            ["encryption"] = encryption,
                            ["flow"] = node.Flow ?? "",
                            ["email"] = email
                        }
                    }
                }
            }
        };
    }

    // 如果是tls加密
    if (security.Equals("tls", StringComparison.OrdinalIgnoreCase))
    {
        string serverName = !string.IsNullOrWhiteSpace(node.Host) ? node.Host : (!string.IsNullOrWhiteSpace(node.Sni) ? node.Sni : node.Address);
        // 如果是vless
        if (node.Protocol.Equals("vless", StringComparison.OrdinalIgnoreCase))
        {
            proxyOutbound["streamSettings"]["tlsSettings"] = new JsonObject()
            {
                ["serverName"] = serverName,
                ["alpn"] = new JsonArray()
                {
                    "h2",
                    "http/1.1",
                    "h3"
                },
                ["fingerprint"] =  "chrome",
                ["echConfigList"] = node.Ech ?? "",
                ["echForceQuery"] = "full"
            };
        }
        // 如果hy2
        if (node.Protocol.Equals("hysteria", StringComparison.OrdinalIgnoreCase))
        {
            proxyOutbound["streamSettings"]["tlsSettings"] = new JsonObject()
            {
                ["serverName"] = serverName,
                ["fingerprint"] =  "chrome",
            };
        }
        
    }
    if (security.Equals("reality", StringComparison.OrdinalIgnoreCase))
    {
        proxyOutbound["streamSettings"]["realitySettings"] = new JsonObject()
        {
            ["serverName"] = node.Sni,
            ["fingerprint"] =  "chrome",
            ["show"] =  false,
            ["publicKey"] =  node.PublicKey,
            ["shortId"] =  node.ShortId,
            ["spiderX"] =  node.Spx,
            ["mldsa65Verify"] =  "",
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
    if (transport.Equals("xhttp", StringComparison.OrdinalIgnoreCase))
    {
        proxyOutbound["streamSettings"]["xhttpSettings"] = new JsonObject()
        {

            ["path"] = node.Path,
            ["host"] = node.Host,
            ["mode"] = "auto",
            ["extra"] =  new JsonObject()
            {
                ["mode"] = "auto",
                ["xPaddingBytes"] = "100-1000"
            }
        };
    }
    if (transport.Equals("hysteria", StringComparison.OrdinalIgnoreCase))
    {
        proxyOutbound["streamSettings"]["hysteriaSettings"] = new JsonObject()
        {
            ["version"] = 2,
            ["auth"] = node.Uuid,
        };
        proxyOutbound["streamSettings"]["finalmask"] = new JsonObject()
        {
            ["udp"] = new JsonArray()
            {
                new JsonObject()
                {
                    ["type"] = node.Obfs,
                    ["settings"] = new JsonObject()
                    {
                        ["password"] = node.ObfsPassword,
                    }
                }
            },
            ["quicParams"] = new JsonObject()
            {
                ["congestion"] = "brutal",
                ["brutalUp"] = "1000mbps",
                ["brutalDown"] = "1000mbps",
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
private JsonObject BuildRouting(bool isGlobalMode, string customProxyRules = "", string customDirectRules = "")
{
    var rulesArray = new JsonArray();

    // 1. Block UDP 443 (QUIC)
    rulesArray.Add(new JsonObject
    {
        ["type"] = "field",
        ["port"] = "443",
        ["network"] = "udp",
        ["outboundTag"] = "block"
    });

    // 2. Custom Proxy Rules (走代理 / 白名单 - 优先级别最高)
    var (customProxyDomains, customProxyIps) = ParseRules(customProxyRules);
    if (customProxyDomains.Count > 0 || customProxyIps.Count > 0)
    {
        var customProxyRule = new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "proxy"
        };
        if (customProxyDomains.Count > 0) customProxyRule["domain"] = customProxyDomains;
        if (customProxyIps.Count > 0) customProxyRule["ip"] = customProxyIps;
        rulesArray.Add(customProxyRule);
    }

    // 3. Custom Direct Rules (不走代理 / 黑名单 / 直连)
    var (customDirectDomains, customDirectIps) = ParseRules(customDirectRules);
    if (customDirectDomains.Count > 0 || customDirectIps.Count > 0)
    {
        var customDirectRule = new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct"
        };
        if (customDirectDomains.Count > 0) customDirectRule["domain"] = customDirectDomains;
        if (customDirectIps.Count > 0) customDirectRule["ip"] = customDirectIps;
        rulesArray.Add(customDirectRule);
    }

    // 4. Built-in Proxy domains
    rulesArray.Add(new JsonObject
    {
        ["type"] = "field",
        ["outboundTag"] = "proxy",
        ["domain"] = new JsonArray
        {
            "geosite:google",
            "domain:cachefly.net",
            "domain:cachefly.com",
            "domain:ip-api.com",
            "domain:ip.sb",
            "domain:ipify.org"
        }
    });

    // 5. Built-in Direct private rules
    rulesArray.Add(new JsonObject
    {
        ["type"] = "field",
        ["outboundTag"] = "direct",
        ["ip"] = new JsonArray { "geoip:private" }
    });
    rulesArray.Add(new JsonObject
    {
        ["type"] = "field",
        ["outboundTag"] = "direct",
        ["domain"] = new JsonArray { "geosite:private" }
    });

    // 6. Global mode or Smart Split mode
    if (isGlobalMode)
    {
        rulesArray.Add(new JsonObject
        {
            ["type"] = "field",
            ["port"] = "0-65535",
            ["outboundTag"] = "proxy"
        });
    }
    else
    {
        // Direct DNS servers IPs
        rulesArray.Add(new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct",
            ["ip"] = new JsonArray
            {
                "223.5.5.5", "223.6.6.6", "2400:3200::1", "2400:3200:baba::1",
                "119.29.29.29", "1.12.12.12", "120.53.53.53", "2402:4e00::",
                "2402:4e00:1::", "180.76.76.76", "2400:da00::6666", "114.114.114.114",
                "114.114.115.115", "114.114.114.119", "114.114.115.119", "114.114.114.110",
                "114.114.115.110", "180.184.1.1", "180.184.2.2", "101.226.4.6",
                "218.30.118.6", "123.125.81.6", "140.207.198.6", "1.2.4.8",
                "210.2.4.8", "52.80.66.66", "117.50.22.22", "2400:7fc0:849e:200::4",
                "2404:c2c0:85d8:901::4", "117.50.10.10", "52.80.52.52", "2400:7fc0:849e:200::8",
                "2404:c2c0:85d8:901::8", "117.50.60.30", "52.80.60.30"
            }
        });

        // Direct DNS domains
        rulesArray.Add(new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct",
            ["domain"] = new JsonArray
            {
                "domain:alidns.com", "domain:doh.pub", "domain:dot.pub",
                "domain:360.cn", "domain:onedns.net"
            }
        });

        // geoip:cn & geosite:cn -> direct
        rulesArray.Add(new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct",
            ["ip"] = new JsonArray { "geoip:cn" }
        });
        rulesArray.Add(new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct",
            ["domain"] = new JsonArray { "geosite:cn" }
        });
    }

    // 7. Inbound DNS tag routing
    rulesArray.Add(new JsonObject
    {
        ["type"] = "field",
        ["inboundTag"] = new JsonArray { "direct-dns-1", "direct-dns-2", "direct-dns-3" },
        ["outboundTag"] = "direct"
    });
    rulesArray.Add(new JsonObject
    {
        ["type"] = "field",
        ["inboundTag"] = new JsonArray { "dns-module" },
        ["outboundTag"] = "proxy"
    });

    return new JsonObject
    {
        ["domainStrategy"] = "AsIs",
        ["rules"] = rulesArray
    };
}

private (JsonArray domains, JsonArray ips) ParseRules(string rulesText)
{
    var domains = new JsonArray();
    var ips = new JsonArray();

    if (string.IsNullOrWhiteSpace(rulesText))
        return (domains, ips);

    string[] lines = rulesText.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    foreach (var rawLine in lines)
    {
        string line = rawLine.Trim();
        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
            continue;

        if (line.StartsWith("geoip:", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("ip:", StringComparison.OrdinalIgnoreCase))
        {
            ips.Add(line);
        }
        else if (IsIpOrCidr(line))
        {
            ips.Add(line);
        }
        else
        {
            if (line.StartsWith("geosite:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("domain:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("full:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("regexp:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("keyword:", StringComparison.OrdinalIgnoreCase))
            {
                domains.Add(line);
            }
            else
            {
                domains.Add($"domain:{line}");
            }
        }
    }

    return (domains, ips);
}

private bool IsIpOrCidr(string input)
{
    if (string.IsNullOrWhiteSpace(input)) return false;

    string ipPart = input;
    int slashIndex = input.IndexOf('/');
    if (slashIndex > 0)
    {
        ipPart = input.Substring(0, slashIndex);
    }

    return System.Net.IPAddress.TryParse(ipPart, out _);
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
            Thread.Sleep(100);

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
                ChildProcessTracker.AddProcess(_xrayProcess);
                AppLogger.LogSystem($"[XRAY核心] Xray 进程已成功启动并绑定 JobObject，PID: {_xrayProcess.Id}");

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
            AppLogger.LogSystem("[XRAY核心] 正在停止 Xray 内核进程及子进程树...");
            if (_xrayProcess != null)
            {
                if (!_xrayProcess.HasExited)
                {
                    _xrayProcess.Kill(true);
                    _xrayProcess.WaitForExit(1000);
                }
                _xrayProcess.Dispose();
                _xrayProcess = null;
            }
            KillAllXrayProcesses();
            AppLogger.LogSystem("[XRAY核心] Xray 内核进程已安全停止。");
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
