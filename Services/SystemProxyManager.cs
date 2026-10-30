using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace chrelyonly_xray_win.Services;

public static class SystemProxyManager
{
    [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    private const string RegKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public static string GetLocalLanIp()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var props = ni.GetIPProperties();
                    foreach (var ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[局域网IP] 获取本地 IPv4 失败: {ex.Message}");
        }
        return "127.0.0.1";
    }

    public static (bool isProxyActive, string proxyServer, bool isOwnProxy) CheckSystemProxyConflict(int ourHttpPort = 10909)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, false);
            if (key != null)
            {
                var val = key.GetValue("ProxyEnable");
                if (val != null && Convert.ToInt32(val) == 1)
                {
                    string proxyServer = key.GetValue("ProxyServer")?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(proxyServer))
                    {
                        bool isOwn = proxyServer.Contains($":{ourHttpPort}");
                        return (true, proxyServer, isOwn);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[系统代理] 检测代理冲突发生异常: {ex.Message}");
        }
        return (false, "", true);
    }

    public static bool GetSystemProxyStatus()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, false);
            if (key != null)
            {
                var val = key.GetValue("ProxyEnable");
                if (val != null && int.TryParse(val.ToString(), out int enabled))
                {
                    return enabled == 1;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[系统代理] 读取系统代理状态失败: {ex.Message}");
        }
        return false;
    }

    public static bool SetSystemProxy(bool enable, string proxyAddress = "127.0.0.1:10909")
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true);
            if (key != null)
            {
                if (enable)
                {
                    key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                    key.SetValue("ProxyServer", proxyAddress, RegistryValueKind.String);
                    key.SetValue("ProxyOverride", "<local>", RegistryValueKind.String);
                    AppLogger.LogSystem($"[系统代理] 已开启 Windows 系统代理: {proxyAddress}");
                }
                else
                {
                    key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                    AppLogger.LogSystem("[系统代理] 已释放并关闭 Windows 系统代理");
                }

                // Refresh Windows WinINet Internet options immediately
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                return true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[系统代理] 设置系统代理发生异常: {ex.Message}");
        }
        return false;
    }

    public static bool GetAutoStartStatus()
    {
        try
        {
            string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, false);
            if (key != null)
            {
                string appName = "BerryXrayWin";
                return key.GetValue(appName) != null;
            }
        }
        catch { }
        return false;
    }

    public static bool SetAutoStart(bool enable)
    {
        try
        {
            string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key != null)
            {
                string appName = "BerryXrayWin";
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (enable)
                {
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(appName, $"\"{exePath}\"");
                        AppLogger.LogSystem("[系统设置] 已开启开机自启动。");
                    }
                }
                else
                {
                    key.DeleteValue(appName, false);
                    AppLogger.LogSystem("[系统设置] 已关闭开机自启动。");
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSystem($"[系统设置] 设置开机自启异常: {ex.Message}");
        }
        return false;
    }
}
