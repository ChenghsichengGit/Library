namespace Library.Application.Abstractions;

/// <summary>
/// 封面圖片的存放位置。資料庫只存 key，網址由實作組出來，換成雲端儲存時資料不用改。
/// </summary>
public interface ICoverStorage
{
    /// <summary>存一張圖，回傳它的 key（檔名）。</summary>
    Task<string> SaveAsync(byte[] content, string extension);

    /// <summary>刪掉一張圖；不存在就不做事。</summary>
    Task DeleteAsync(string key);

    /// <summary>把 key 轉成可以讀取的網址。</summary>
    string GetUrl(string key);
}
