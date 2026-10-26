using System.Diagnostics;
using System.IO;

namespace chrelyonly_xray_win.Services;

public class XrayCoreManager
{
    private Process? _xrayProcess;

    public string CoreFolderPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core");
    public string XrayExePath => Path.Combine(CoreFolderPath, "xray.exe");
    public string ConfigPath => Path.Combine(CoreFolderPath, "config.json");

    public bool IsRunning => _xrayProcess != null && !_xrayProcess.HasExited;

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
                // Xray binary not yet placed in Core/ folder
                return false;
            }

            // Write temporary config file
            File.WriteAllText(ConfigPath, configJson);

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
            return _xrayProcess != null;
        }
        catch
        {
            return false;
        }
    }

    public void StopCore()
    {
        try
        {
            if (_xrayProcess != null && !_xrayProcess.HasExited)
            {
                _xrayProcess.Kill();
                _xrayProcess.Dispose();
                _xrayProcess = null;
            }
        }
        catch { }
    }
}
