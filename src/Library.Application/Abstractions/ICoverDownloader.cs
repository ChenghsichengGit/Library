namespace Library.Application.Abstractions;

/// <summary>
/// 從網址下載封面圖片。實作放在 Infrastructure。
/// </summary>
public interface ICoverDownloader
{
    /// <summary>超過 maxBytes 丟出 InvalidCoverException；連線失敗、逾時照常丟出例外。</summary>
    Task<byte[]> DownloadAsync(Uri url, int maxBytes, CancellationToken cancellationToken);
}
