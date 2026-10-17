using Library.Application.Abstractions;
using Library.Application.Exceptions;

namespace Library.Infrastructure.Covers;

/// <summary>
/// 用 HttpClient 下載封面，邊讀邊計算大小，超過上限就停止，不會把大檔案整個讀進記憶體。
/// </summary>
public class HttpCoverDownloader(HttpClient http) : ICoverDownloader
{
    public async Task<byte[]> DownloadAsync(Uri url, int maxBytes, CancellationToken cancellationToken)
    {
        // ResponseHeadersRead：收到表頭就返回，body 還沒下載
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // 對方有說大小而且超過 → 不用下載就拒絕
        if (response.Content.Headers.ContentLength > maxBytes)
            throw new InvalidCoverException("封面不能超過 10MB");

        // 對方可能沒說大小或說謊，所以讀取時也要計算
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
                throw new InvalidCoverException("封面不能超過 10MB");
        }

        return buffer.ToArray();
    }
}
