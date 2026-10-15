using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 作品的一個商店網址，對應資料表 WorkUrls。購買狀態、語言、價格各網址分開記錄。
/// </summary>
public class WorkUrl
{
    public int Id { get; set; }

    public int WorkId { get; set; }

    public Work Work { get; set; } = null!;

    /// <summary>在作品網址清單中的順序，0 是主要網址。</summary>
    public int Position { get; set; }

    [MaxLength(2000)]
    public string Url { get; set; } = "";

    public bool Purchased { get; set; }

    /// <summary>這個網址是哪個語言版本；null 代表未指定。</summary>
    public UrlLanguage? Language { get; set; }

    /// <summary>原幣別的價格；null 代表沒有價格（和免費不同）。</summary>
    public decimal? Price { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "";
}
