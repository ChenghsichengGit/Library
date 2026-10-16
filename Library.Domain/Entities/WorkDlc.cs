using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 作品的 DLC／追加內容（特典、續篇…），對應資料表 WorkDlcs。不影響作品的「已購買」，也不參與重複偵測。
/// </summary>
public class WorkDlc
{
    public int Id { get; set; }

    public int WorkId { get; set; }

    public Work Work { get; set; } = null!;

    /// <summary>在作品 DLC 清單中的順序。</summary>
    public int Position { get; set; }

    [MaxLength(500)]
    public string Name { get; set; } = "";

    /// <summary>可省略，沒有時是空字串。</summary>
    [MaxLength(2000)]
    public string Url { get; set; } = "";

    public bool Purchased { get; set; }

    /// <summary>原幣別的價格；null 代表沒有價格。</summary>
    public decimal? Price { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "";
}
