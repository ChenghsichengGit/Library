namespace Library.Application.Covers;

/// <summary>
/// 用檔案開頭的魔術位元組判斷圖片格式。不相信前端送來的檔名和 Content-Type。
/// </summary>
public static class CoverImageFormat
{
    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> Gif => "GIF8"u8;

    // WebP 是 RIFF 開頭、第 8～11 個位元組是 WEBP；中間 4 個位元組是檔案大小
    private static ReadOnlySpan<byte> Riff => "RIFF"u8;
    private static ReadOnlySpan<byte> Webp => "WEBP"u8;

    /// <summary>回傳副檔名（".jpg"、".png"、".gif"、".webp"）；不是支援的格式回傳 null。</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(Jpeg))
            return ".jpg";
        if (header.StartsWith(Png))
            return ".png";
        if (header.StartsWith(Gif))
            return ".gif";
        if (header.Length >= 12 && header.StartsWith(Riff) && header[8..12].SequenceEqual(Webp))
            return ".webp";

        return null;
    }
}
