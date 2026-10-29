using System.Configuration;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using chrelyonly_xray_win.Services;

using Application = System.Windows.Application;

namespace chrelyonly_xray_win;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private static EventWaitHandle? _eventWaitHandle;
    private const string AppUniqueName = "chrelyonly_xray_win_single_instance_guid";

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, AppUniqueName + "_mutex", out bool createdNew);
        if (!createdNew)
        {
            // Already running, signal existing instance to foreground and exit silently
            try
            {
                _eventWaitHandle = EventWaitHandle.OpenExisting(AppUniqueName + "_event");
                _eventWaitHandle?.Set();
            }
            catch
            {
                // Ignored
            }
            Shutdown();
            return;
        }

        // 注册全局异常处理及进程退出逻辑，确保非正常退出时释放系统代理和终止内核
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        // 启动时主动扫描并强制清理上次残留/孤儿 xraylib 进程
        XrayCoreManager.KillAllXrayProcesses();

        _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, AppUniqueName + "_event");
        Task.Run(() =>
        {
            while (true)
            {
                try
                {
                    _eventWaitHandle.WaitOne();
                    Current?.Dispatcher.Invoke(() =>
                    {
                        if (Current.MainWindow is MainWindow mainWindow)
                        {
                            mainWindow.Show();
                            if (mainWindow.WindowState == WindowState.Minimized)
                            {
                                mainWindow.WindowState = WindowState.Normal;
                            }
                            mainWindow.Activate();
                            mainWindow.Topmost = true;
                            mainWindow.Topmost = false;
                            mainWindow.Focus();
                        }
                    });
                }
                catch
                {
                    break;
                }
            }
        });

        base.OnStartup(e);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            AppLogger.LogSystem($"[全局异常] AppDomain 发生未处理异常: {e.ExceptionObject}");
            CleanupOnExit();
        }
        catch { }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            AppLogger.LogSystem($"[全局异常] Dispatcher 发生未处理异常: {e.Exception.Message}");
            CleanupOnExit();
        }
        catch { }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            AppLogger.LogSystem($"[全局异常] Task 发生未观察异常: {e.Exception.Message}");
            e.SetObserved();
        }
        catch { }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            CleanupOnExit();
        }
        catch { }
    }

    private static void CleanupOnExit()
    {
        try
        {
            SystemProxyManager.SetSystemProxy(false);
            XrayCoreManager.KillAllXrayProcesses();
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CleanupOnExit();

        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // Ignored
            }
            _mutex.Dispose();
        }
        _eventWaitHandle?.Dispose();
        base.OnExit(e);
    }
}
