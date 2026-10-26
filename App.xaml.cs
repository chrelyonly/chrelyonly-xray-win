using System.Configuration;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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

    protected override void OnExit(ExitEventArgs e)
    {
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
