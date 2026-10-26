using System.Diagnostics;
using System.IO;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public class XrayCoreManager
{
    private Process? _xrayProcess;

    public string CoreFolderPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core");
    public string XrayExePath => Path.Combine(CoreFolderPath, "xray.exe");
    public string ConfigPath => Path.Combine(CoreFolderPath, "config.json");
    public string LogFilePath => Path.Combine(CoreFolderPath, "xray.log");

    public bool IsRunning => _xrayProcess != null && !_xrayProcess.HasExited;

    public string GenerateClientConfig(ServerNode node)
    {
        AppLogger.LogSystem($"[XRAY核心] 正在为节点「{node.Name}」({node.Address}:{node.Port}, 协议: {node.Protocol}) 生成客户端配置...");
        string uuid = string.IsNullOrEmpty(node.Uuid) ? "d342d11e-d424-4583-b36e-524abb1f0cf4" : node.Uuid;
        string address = node.Address;
        int port = node.Port;
        string sni = string.IsNullOrEmpty(node.Sni) ? address : node.Sni;
        string flow = node.Flow;
        string protocol = node.Protocol.ToLower();
        if (protocol != "vmess" && protocol != "trojan" && protocol != "shadowsocks")
        {
            protocol = "vless";
        }

        var configJson = $@"{{
  ""log"": {{
    ""loglevel"": ""warning""
  }},
  ""inbounds"": [
    {{
      ""tag"": ""socks-in"",
      ""port"": 10808,
      ""listen"": ""127.0.0.1"",
      ""protocol"": ""socks"",
      ""settings"": {{
        ""auth"": ""noauth"",
        ""udp"": true
      }}
    }},
    {{
      ""tag"": ""http-in"",
      ""port"": 10809,
      ""listen"": ""127.0.0.1"",
      ""protocol"": ""http"",
      ""settings"": {{}}
    }}
  ],
  ""outbounds"": [
    {{
      ""tag"": ""proxy"",
      ""protocol"": ""{protocol}"",
      ""settings"": {{
        ""vnext"": [
          {{
            ""address"": ""{address}"",
            ""port"": {port},
            ""users"": [
              {{
                ""id"": ""{uuid}"",
                ""flow"": ""{flow}"",
                ""encryption"": ""none"",
                ""level"": 0
              }}
            ]
          }}
        ]
      }},
      ""streamSettings"": {{
        ""network"": ""{node.Transport.ToLower()}"",
        ""security"": ""{node.Security.ToLower()}"",
        ""realitySettings"": {{
          ""show"": false,
          ""fingerprint"": ""chrome"",
          ""serverName"": ""{sni}"",
          ""publicKey"": ""{node.PublicKey}"",
          ""shortId"": ""{node.ShortId}""
        }},
        ""tcpSettings"": {{}},
        ""wsSettings"": {{}}
      }}
    }},
    {{
      ""tag"": ""direct"",
      ""protocol"": ""freedom"",
      ""settings"": {{}}
    }},
    {{
      ""tag"": ""block"",
      ""protocol"": ""blackhole"",
      ""settings"": {{}}
    }}
  ]
}}";
        AppLogger.LogSystem($"[XRAY核心] 客户端配置文件生成成功，路径: {ConfigPath}");
        return configJson;
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
                AppLogger.LogSystem($"[XRAY核心] 错误：未在指定路径找到 xray.exe，路径: {XrayExePath}");
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
