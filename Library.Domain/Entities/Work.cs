using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 作品，對應資料表 Works。
/// </summary>
public class Work
{
    public int Id { get; set; }

    /// <summary>主要名稱：中 → 日 → 英取第一個有值的。由 SQL Server 的計算欄位產生，程式不能設定。</summary>
    [MaxLength(300)]
    public string Title { get; private set; } = "";

    // 沒填用空字串表示；CHECK 約束保證三個至少有一個不是空字串
    [MaxLength(300)]
    public string TitleZh { get; set; } = "";

    [MaxLength(300)]
    public string TitleJa { get; set; } = "";

    [MaxLength(300)]
    public string TitleEn { get; set; } = "";

    /// <summary>簡介：抓取時帶入商店的介紹（只在表單是空的時候），也可以自己寫。</summary>
    [MaxLength(2000)]
    public string Description { get; set; } = "";

    public DateOnly? ReleaseDate { get; set; }

    /// <summary>評分：0 = 未評價，1～5 = 星數，6 = 王冠。</summary>
    public int Score { get; set; }

    public bool Favorite { get; set; }

    /// <summary>加入收藏的時間（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最後一次新增、修改或刪除的時間（UTC），用來依修改時間排序。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>軟刪除的時間；有值表示在「最近刪除」裡。</summary>
    public DateTime? DeletedAt { get; set; }

    [MaxLength(100)]
    public string CoverKey { get; set; } = "";

    /// <summary>作者與社團，以 WorkCreator.Role 區分。</summary>
    public List<WorkCreator> Creators { get; set; } = [];

    public int WorkTypeId { get; set; }

    public WorkType WorkType { get; set; } = null!;

    public List<Tag> Tags { get; set; } = [];

    /// <summary>null 代表沒有系列。</summary>
    public int? SeriesId { get; set; }

    public Series? Series { get; set; }

    /// <summary>商店網址，依 Position 排序；作品的「已購買」由任一網址已購買推導。</summary>
    public List<WorkUrl> Urls { get; set; } = [];

    /// <summary>DLC／追加內容，依 Position 排序；不影響作品的「已購買」。</summary>
    public List<WorkDlc> Dlcs { get; set; } = [];
}
