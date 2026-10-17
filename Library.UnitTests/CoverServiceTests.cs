using System.Text;
using Library.Application.Abstractions;
using Library.Application.Exceptions;
using Library.Application.Services;

namespace Library.UnitTests;

/// <summary>
/// CoverService 的單元測試：storage 和 downloader 換成假的，只驗證規則和呼叫順序。
/// </summary>
public class CoverServiceTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] Html = Encoding.UTF8.GetBytes("<html><body>router admin</body></html>");

    private class FakeStorage : ICoverStorage
    {
        public List<string> SavedExtensions { get; } = [];

        public Task<string> SaveAsync(byte[] content, string extension)
        {
            SavedExtensions.Add(extension);
            return Task.FromResult("key" + extension);
        }

        public Task DeleteAsync(string key) => Task.CompletedTask;
        public string GetUrl(string key) => key;
    }

    // 回傳指定的內容，並記下被呼叫時的參數
    private class FakeDownloader(byte[] content) : ICoverDownloader
    {
        public int? ReceivedMaxBytes { get; private set; }

        public Task<byte[]> DownloadAsync(Uri url, int maxBytes, CancellationToken cancellationToken)
        {
            ReceivedMaxBytes = maxBytes;
            return Task.FromResult(content);
        }
    }

    private readonly FakeStorage _storage = new();

    private CoverService CreateService(byte[]? downloaded = null) => new(_storage, new FakeDownloader(downloaded ?? Png));

    [Fact]
    public async Task SaveAsync_ValidImage_SavesWithDetectedExtension()
    {
        var key = await CreateService().SaveAsync(Png);

        Assert.Equal("key.png", key);
        Assert.Equal([".png"], _storage.SavedExtensions);
    }

    // 防：先存檔才檢查，不合法的檔案也留在資料夾裡
    [Fact]
    public async Task SaveAsync_TooLarge_ThrowsWithoutSaving()
    {
        var content = new byte[CoverService.MaxBytes + 1];
        Png.CopyTo(content, 0);

        await Assert.ThrowsAsync<InvalidCoverException>(() => CreateService().SaveAsync(content));
        Assert.Empty(_storage.SavedExtensions);
    }

    [Fact]
    public async Task SaveAsync_NotImage_ThrowsWithoutSaving()
    {
        await Assert.ThrowsAsync<InvalidCoverException>(() => CreateService().SaveAsync(Html));
        Assert.Empty(_storage.SavedExtensions);
    }

    // 防：file:// 讀到伺服器上的檔案；要在連線之前就擋下
    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.com/cover.png")]
    public async Task DownloadAsync_NonHttpScheme_ThrowsWithoutDownloading(string url)
    {
        var downloader = new FakeDownloader(Png);
        var service = new CoverService(_storage, downloader);

        await Assert.ThrowsAsync<InvalidCoverException>(() => service.DownloadAsync(new Uri(url), CancellationToken.None));
        Assert.Null(downloader.ReceivedMaxBytes);
    }

    // 防：下載的內容沒經過格式檢查（例如內網的管理頁面）
    [Fact]
    public async Task DownloadAsync_NotImage_ThrowsWithoutSaving()
    {
        var service = CreateService(Html);

        await Assert.ThrowsAsync<InvalidCoverException>(() =>
            service.DownloadAsync(new Uri("http://192.168.1.1/"), CancellationToken.None));
        Assert.Empty(_storage.SavedExtensions);
    }

    [Fact]
    public async Task DownloadAsync_ValidImage_SavesAndPassesMaxBytes()
    {
        var downloader = new FakeDownloader(Png);
        var service = new CoverService(_storage, downloader);

        var key = await service.DownloadAsync(new Uri("https://example.com/cover.png"), CancellationToken.None);

        Assert.Equal("key.png", key);
        Assert.Equal(CoverService.MaxBytes, downloader.ReceivedMaxBytes);
    }
}
