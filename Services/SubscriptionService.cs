using System.Net.Http;
using System.Text;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Services;

public static class SubscriptionService
{
    private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<List<ServerNode>> FetchSubscriptionNodesAsync(string subscriptionUrl)
    {
        var result = new List<ServerNode>();
        if (string.IsNullOrWhiteSpace(subscriptionUrl)) return result;

        try
        {
            var content = await HttpClient.GetStringAsync(subscriptionUrl);
            var decodedContent = TryDecodeBase64(content.Trim());

            var lines = decodedContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var node = ProxyLinkParser.ParseShareLink(trimmed);
                if (node != null)
                {
                    result.Add(node);
                }
            }
        }
        catch
        {
            // Network or parse error fallback
        }

        return result;
    }

    private static string TryDecodeBase64(string input)
    {
        try
        {
            // Handle URL-safe base64 padding
            string padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            var bytes = Convert.FromBase64String(padded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // Not base64, return original input
            return input;
        }
    }
}
