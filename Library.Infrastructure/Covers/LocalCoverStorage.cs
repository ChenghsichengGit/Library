using Library.Application.Abstractions;

namespace Library.Infrastructure.Covers;

/// <summary>
/// 把封面存在本機資料夾，由 API 以 /covers/ 提供。
/// </summary>
public class LocalCoverStorage(string directory) : ICoverStorage
{
    public async Task<string> SaveAsync(byte[] content, string extension)
    {
        // 檔名由伺服器產生，不用前端送來的名字：不會重複，也不會被塞進 ../ 之類的路徑
        var key = Guid.NewGuid().ToString("N") + extension;
        await File.WriteAllBytesAsync(Path.Combine(directory, key), content);
        return key;
    }

    public Task DeleteAsync(string key)
    {
        // SaveWorkRequest 已檢查 key 的格式，這裡再擋一次，避免刪到資料夾外面的檔案
        if (Path.GetFileName(key) != key)
            return Task.CompletedTask;

        File.Delete(Path.Combine(directory, key));
        return Task.CompletedTask;
    }

    public string GetUrl(string key) => $"/covers/{key}";
}
