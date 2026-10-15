namespace Library.Application.Urls;

/// <summary>
/// 把同一頁的不同寫法轉成同一個字串，用來比對網址是否重複。只用於比對，存檔仍存原網址。
/// </summary>
public static class UrlNormalizer
{
    // 前綴比對（同原版）：ref 也會比對到 refresh 這類一般參數
    private static readonly string[] TrackingPrefixes =
        ["utm_", "fbclid", "gclid", "ref", "referrer", "share", "from", "spm", "_ga"];

    /// <summary>
    /// 去協定、網域轉小寫並去掉 www.、去結尾斜線、去追蹤參數並依名稱排序。空白回傳 null，不是網址時回傳小寫的原字串。
    /// </summary>
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        var withScheme = trimmed.Contains("://") ? trimmed : "https://" + trimmed;

        // Invariant／Ordinal：結果不受電腦語系影響
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri))
            return trimmed.ToLowerInvariant();

        // Uri.Host 已經是小寫；路徑保留大小寫，部分網站的路徑區分大小寫（DLsite 的 RJ123456）
        var host = uri.Host;
        if (host.StartsWith("www."))
            host = host[4..];
        var path = uri.AbsolutePath.TrimEnd('/');

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !TrackingPrefixes.Any(t => p.StartsWith(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return query.Count > 0
            ? host + path + "?" + string.Join('&', query)
            : host + path;
    }
}
