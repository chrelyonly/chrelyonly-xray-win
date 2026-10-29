using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace chrelyonly_xray_win.Services;

public class SpeedTestReport
{
    public double ProgressPercentage { get; set; }
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double CurrentSpeedBytesPerSec { get; set; }
    public double PeakSpeedBytesPerSec { get; set; }
    public double AverageSpeedBytesPerSec { get; set; }
    public string StatusText { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class SpeedTester
{
    public const string DefaultTestUrl = "https://cachefly.cachefly.net/50mb.test";

    /// <summary>
    /// 执行节点测速，通过 IProgress 实时汇报测速进度与实时/峰值/平均网速
    /// </summary>
    public static async Task TestDownloadSpeedAsync(
        string testUrl,
        string proxyAddress,
        IProgress<SpeedTestReport> progress,
        CancellationToken cancellationToken)
    {
        var report = new SpeedTestReport
        {
            StatusText = "正在建立代理连接...",
            TotalBytes = 50 * 1024 * 1024 // 默认 50MB
        };
        progress.Report(report);

        HttpClientHandler handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        if (!string.IsNullOrEmpty(proxyAddress))
        {
            handler.Proxy = new System.Net.WebProxy(proxyAddress);
            handler.UseProxy = true;
        }
        else
        {
            handler.UseProxy = false;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        try
        {
            report.StatusText = "正在发起 HTTP 请求...";
            progress.Report(report);

            using var response = await client.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? (50 * 1024 * 1024);
            report.TotalBytes = totalBytes;
            report.StatusText = "正在高速下载数据包...";

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            byte[] buffer = new byte[65536]; // 64 KB buffer

            var overallStopwatch = Stopwatch.StartNew();
            var intervalStopwatch = Stopwatch.StartNew();

            long totalDownloaded = 0;
            long intervalDownloaded = 0;
            double peakSpeed = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0)
                {
                    break; // 下载完成
                }

                totalDownloaded += bytesRead;
                intervalDownloaded += bytesRead;

                double elapsedIntervalSec = intervalStopwatch.Elapsed.TotalSeconds;
                if (elapsedIntervalSec >= 0.25) // 每 250ms 更新一次 UI 速率
                {
                    double currentSpeed = intervalDownloaded / elapsedIntervalSec;
                    if (currentSpeed > peakSpeed)
                    {
                        peakSpeed = currentSpeed;
                    }

                    double totalElapsedSec = overallStopwatch.Elapsed.TotalSeconds;
                    double avgSpeed = totalElapsedSec > 0 ? totalDownloaded / totalElapsedSec : 0;
                    double percent = Math.Min(100.0, (double)totalDownloaded / totalBytes * 100.0);

                    report.ProgressPercentage = percent;
                    report.DownloadedBytes = totalDownloaded;
                    report.CurrentSpeedBytesPerSec = currentSpeed;
                    report.PeakSpeedBytesPerSec = peakSpeed;
                    report.AverageSpeedBytesPerSec = avgSpeed;
                    report.StatusText = $"正在测速: {percent:F1}%";

                    progress.Report(report);

                    intervalDownloaded = 0;
                    intervalStopwatch.Restart();
                }

                // 安全上限限制：若已超过 50MB 则结束
                if (totalDownloaded >= totalBytes)
                {
                    break;
                }
            }

            overallStopwatch.Stop();
            double finalTotalSec = overallStopwatch.Elapsed.TotalSeconds;
            double finalAvgSpeed = finalTotalSec > 0 ? totalDownloaded / finalTotalSec : 0;

            report.ProgressPercentage = 100.0;
            report.DownloadedBytes = totalDownloaded;
            report.CurrentSpeedBytesPerSec = 0;
            report.PeakSpeedBytesPerSec = peakSpeed;
            report.AverageSpeedBytesPerSec = finalAvgSpeed;
            report.IsCompleted = true;
            report.StatusText = "测速完成！";

            progress.Report(report);
        }
        catch (OperationCanceledException)
        {
            report.IsFailed = false;
            report.StatusText = "测速已取消";
            progress.Report(report);
        }
        catch (Exception ex)
        {
            report.IsFailed = true;
            report.ErrorMessage = ex.Message;
            report.StatusText = $"测速失败: {ex.Message}";
            progress.Report(report);
        }
    }
}
