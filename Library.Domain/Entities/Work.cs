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

    [MaxLength(300)]
    public string Remark { get; set; } = "";

    public DateOnly? ReleaseDate { get; set; }

    /// <summary>評分：0 = 未評價，1～5 = 星數，6 = 王冠。</summary>
    public int Score { get; set; }

    public bool Favorite { get; set; }

    /// <summary>已購買。暫時的欄位，之後改成由各商店網址的購買狀態推導。</summary>
    public bool Purchased { get; set; }

    /// <summary>加入收藏的時間（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>軟刪除的時間；有值表示在「最近刪除」裡。</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>作者與社團，以 WorkCreator.Role 區分。</summary>
    public List<WorkCreator> Creators { get; set; } = [];

    public int WorkTypeId { get; set; }

    public WorkType WorkType { get; set; } = null!;
}
