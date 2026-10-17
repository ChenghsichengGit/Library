using System.Net;
using Library.Application.Exceptions;
using Library.Infrastructure.Covers;

namespace Library.UnitTests;

/// <summary>
/// HttpCoverDownloader 的單元測試：用假的 HttpMessageHandler 取代網路，上限用 1000 bytes 就不必產生 10MB 的資料。
/// </summary>
public class HttpCoverDownloaderTests
{
    private const int MaxBytes = 1000;
    private static readonly Uri Url = new("https://example.com/cover.jpg");

    // contentLength 不給時用實際長度；給 null 代表回應沒有 Content-Length；給其他值代表對方報的大小和實際不同
    private class FakeHandler(HttpStatusCode status, byte[] body, long? contentLength, bool overrideLength)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(body);
            if (overrideLength)
                content.Headers.ContentLength = contentLength;

            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }

    private static HttpCoverDownloader CreateDownloader(byte[] body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new FakeHandler(status, body, null, false)));

    private static HttpCoverDownloader CreateDownloader(byte[] body, long? contentLength) =>
        new(new HttpClient(new FakeHandler(HttpStatusCode.OK, body, contentLength, true)));

    [Fact]
    public async Task DownloadAsync_ReturnsBody()
    {
        var body = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

        var result = await CreateDownloader(body).DownloadAsync(Url, MaxBytes, CancellationToken.None);

        Assert.Equal(body, result);
    }

    // 防：用 >= 比較，剛好等於上限的圖片被擋掉
    [Fact]
    public async Task DownloadAsync_ExactlyMaxBytes_Passes()
    {
        var result = await CreateDownloader(new byte[MaxBytes]).DownloadAsync(Url, MaxBytes, CancellationToken.None);

        Assert.Equal(MaxBytes, result.Length);
    }

    [Fact]
    public async Task DownloadAsync_ContentLengthTooLarge_Throws()
    {
        var downloader = CreateDownloader(new byte[MaxBytes + 1]);

        await Assert.ThrowsAsync<InvalidCoverException>(() => downloader.DownloadAsync(Url, MaxBytes, CancellationToken.None));
    }

    // 防：只看 Content-Length。對方沒給大小，或報得比實際小時，要靠讀取時的計算擋下
    [Theory]
    [InlineData(null)]
    [InlineData(10L)]
    public async Task DownloadAsync_ActualBodyTooLarge_Throws(long? reportedLength)
    {
        var downloader = CreateDownloader(new byte[MaxBytes * 3], reportedLength);

        await Assert.ThrowsAsync<InvalidCoverException>(() => downloader.DownloadAsync(Url, MaxBytes, CancellationToken.None));
    }

    // 防：漏了 EnsureSuccessStatusCode，把 404 頁面當成圖片內容
    [Fact]
    public async Task DownloadAsync_NotFound_ThrowsHttpRequestException()
    {
        var downloader = CreateDownloader("Not Found"u8.ToArray(), HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<HttpRequestException>(() => downloader.DownloadAsync(Url, MaxBytes, CancellationToken.None));
    }
}
