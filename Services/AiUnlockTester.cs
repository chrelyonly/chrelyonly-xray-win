using System.Diagnostics;
using System.Net;
using System.Net.Http;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public static class AiUnlockTester
{
    private static HttpClient CreateHttpClient(bool isConnected)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        if (isConnected)
        {
            handler.Proxy = new WebProxy("http://127.0.0.1:10809");
            handler.UseProxy = true;
        }
        else
        {
            handler.UseProxy = false;
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(4)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        return client;
    }

    public static async Task<(UnlockStatus status, int latencyMs)> TestPlatformAsync(string key, bool isConnected)
    {
        var stopwatch = Stopwatch.StartNew();
        using var client = CreateHttpClient(isConnected);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));

        try
        {
            string url = key switch
            {
                "OpenAI" => "https://ios.chat.openai.com/public-api/mobile-sdk/telemetry",
                "Claude" => "https://claude.ai/login",
                "Gemini" => "https://gemini.google.com",
                "YouTube" => "https://www.youtube.com/premium",
                "Google" => "https://www.google.com/generate_204",
                _ => "https://www.google.com/generate_204"
            };

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            stopwatch.Stop();
            int ms = (int)stopwatch.ElapsedMilliseconds;

            int code = (int)response.StatusCode;

            switch (key)
            {
                case "OpenAI":
                    if (code == 200 || code == 400 || code == 405 || code == 302 || code == 301)
                    {
                        return (UnlockStatus.Passed, ms);
                    }
                    if (code == 403)
                    {
                        return (UnlockStatus.Failed, ms);
                    }
                    string openAiBody = await response.Content.ReadAsStringAsync(cts.Token);
                    if (openAiBody.Contains("unsupported_country") || openAiBody.Contains("VPN") || openAiBody.Contains("Access denied"))
                    {
                        return (UnlockStatus.Failed, ms);
                    }
                    return (UnlockStatus.Passed, ms);

                case "Claude":
                    if (code == 200 || code == 302 || code == 301)
                    {
                        string claudeBody = await response.Content.ReadAsStringAsync(cts.Token);
                        if (claudeBody.Contains("App unavailable in your region") || claudeBody.Contains("403 Forbidden"))
                        {
                            return (UnlockStatus.Failed, ms);
                        }
                        return (UnlockStatus.Passed, ms);
                    }
                    return (UnlockStatus.Failed, ms);

                case "Gemini":
                    if (code == 200 || code == 302 || code == 301)
                    {
                        return (UnlockStatus.Passed, ms);
                    }
                    return (UnlockStatus.Failed, ms);

                case "YouTube":
                    if (code == 200 || code == 302 || code == 301)
                    {
                        return (UnlockStatus.Passed, ms);
                    }
                    return (UnlockStatus.Failed, ms);

                case "Google":
                default:
                    if (code == 204 || code == 200 || code == 302 || code == 301)
                    {
                        return (UnlockStatus.Passed, ms);
                    }
                    return (UnlockStatus.Failed, ms);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            AppLogger.LogSystem($"[AI检测] 检测 {key} 异常/超时: {ex.Message}");
            return (UnlockStatus.Failed, 0);
        }
    }
}
