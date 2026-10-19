using Library.Application.Abstractions;

namespace Library.IntegrationTests.Support;

/// <summary>
/// 不碰磁碟的 ICoverStorage：記下被刪的 key，網址加上 fake/ 前綴，測試才看得出網址是由 storage 組出來的。
/// </summary>
public class FakeCoverStorage : ICoverStorage
{
    public List<string> Deleted { get; } = [];

    public Task<string> SaveAsync(byte[] content, string extension) => throw new NotSupportedException();

    public Task DeleteAsync(string key)
    {
        Deleted.Add(key);
        return Task.CompletedTask;
    }

    public string GetUrl(string key) => $"fake/{key}";
}
