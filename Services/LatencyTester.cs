using System.Diagnostics;
using System.Net.Sockets;

namespace chrelyonly_xray_win.Services;

public static class LatencyTester
{
    public static async Task<int> TestTcpLatencyAsync(string host, int port, int timeoutMs = 3000)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMs);

            var completedTask = await Task.WhenAny(connectTask, delayTask);
            stopwatch.Stop();

            if (completedTask == connectTask && client.Connected)
            {
                return (int)stopwatch.ElapsedMilliseconds;
            }
            return 0; // Timeout
        }
        catch
        {
            return 0; // Error / Timeout
        }
    }
}
