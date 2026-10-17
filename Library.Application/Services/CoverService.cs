using Library.Application.Abstractions;
using Library.Application.Covers;
using Library.Application.Exceptions;

namespace Library.Application.Services;

/// <summary>
/// 封面的規則：檢查大小和格式後交給 ICoverStorage 存檔；從網址下載時只允許 http/https。
/// </summary>
public class CoverService(ICoverStorage storage, ICoverDownloader downloader)
{
    public const int MaxBytes = 10 * 1024 * 1024; // 10MB

    /// <summary>檢查後存檔，回傳 key。不合法時丟出 InvalidCoverException。</summary>
    public async Task<string> SaveAsync(byte[] content)
    {
        if (content.Length > MaxBytes)
            throw new InvalidCoverException("封面不能超過 10MB");

        // 看內容判斷格式，副檔名改過的檔案騙不過去
        var extension = CoverImageFormat.DetectExtension(content);
        if (extension == null)
            throw new InvalidCoverException("只支援 JPG、PNG、GIF、WebP");

        return await storage.SaveAsync(content, extension);
    }

    /// <summary>從網址下載圖片並存檔，回傳 key。</summary>
    public async Task<string> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        // 只允許 http、https，不允許 file:// 這類讀取本機的網址
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            throw new InvalidCoverException("只支援 http、https 網址");

        var content = await downloader.DownloadAsync(url, MaxBytes, cancellationToken);
        // 下載來的內容也要檢查格式，內網的 HTML 頁面存不進來
        return await SaveAsync(content);
    }
}
