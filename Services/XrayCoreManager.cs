using System.Diagnostics;
using System.IO;
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
        return XrayConfigBuilder.GenerateClientConfig(node, allowLan, isGlobalMode, customProxyRules, customDirectRules, ConfigPath);
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
