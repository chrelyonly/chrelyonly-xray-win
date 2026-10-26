using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

using Application = System.Windows.Application;

namespace chrelyonly_xray_win.Services;

public static class AppLogger
{
    public static ObservableCollection<string> SystemLogLines { get; } = new();
    public static ObservableCollection<string> CoreLogLines { get; } = new();

    public static bool IsLoggingEnabled { get; set; } = true;

    public static void LogSystem(string message)
    {
        if (!IsLoggingEnabled) return;
        string formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Debug.WriteLine(formatted);
        EnqueueLog(SystemLogLines, formatted);
    }

    public static void LogCore(string message)
    {
        if (!IsLoggingEnabled) return;
        string formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Debug.WriteLine(formatted);
        EnqueueLog(CoreLogLines, formatted);
    }

    private static void EnqueueLog(ObservableCollection<string> targetCollection, string line)
    {
        try
        {
            if (Application.Current?.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    AddLine(targetCollection, line);
                }
                else
                {
                    Application.Current.Dispatcher.InvokeAsync(() => AddLine(targetCollection, line));
                }
            }
        }
        catch
        {
            // Fail-safe
        }
    }

    private static void AddLine(ObservableCollection<string> collection, string line)
    {
        collection.Add(line);
        while (collection.Count > 100)
        {
            collection.RemoveAt(0);
        }
    }
}
