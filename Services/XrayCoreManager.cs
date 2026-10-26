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

        return $@"{{
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
    }

    public bool StartCore(string configJson)
    {
        try
        {
            if (!Directory.Exists(CoreFolderPath))
            {
                Directory.CreateDirectory(CoreFolderPath);
            }

            if (!File.Exists(XrayExePath))
            {
                Debug.WriteLine("[XRAY] ERROR: xray.exe not found at " + XrayExePath);
                return false;
            }

            File.WriteAllText(ConfigPath, configJson);
            Debug.WriteLine("[XRAY] Config written to " + ConfigPath);

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
                _xrayProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string logLine = $"[{DateTime.Now:HH:mm:ss}][OUT] {e.Data}";
                        Debug.WriteLine(logLine);
                        AppendLogFile(logLine);
                    }
                };

                _xrayProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string logLine = $"[{DateTime.Now:HH:mm:ss}][ERR] {e.Data}";
                        Debug.WriteLine(logLine);
                        AppendLogFile(logLine);
                    }
                };

                _xrayProcess.BeginOutputReadLine();
                _xrayProcess.BeginErrorReadLine();
                Debug.WriteLine("[XRAY] Core started successfully.");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[XRAY] StartCore Exception: " + ex.Message);
            return false;
        }
    }

    public void StopCore()
    {
        try
        {
            if (_xrayProcess != null)
            {
                Debug.WriteLine("[XRAY] Stopping core process...");
                if (!_xrayProcess.HasExited)
                {
                    _xrayProcess.Kill(true);
                }
                _xrayProcess.Dispose();
                _xrayProcess = null;
                Debug.WriteLine("[XRAY] Core stopped.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[XRAY] StopCore Exception: " + ex.Message);
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
                    if (fi.Length > 5 * 1024 * 1024) // 5 MB size limit to prevent disk overflow
                    {
                        File.WriteAllText(LogFilePath, "");
                    }
                }
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch { }
    }
}
