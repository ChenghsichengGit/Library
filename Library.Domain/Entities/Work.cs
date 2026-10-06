using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 作品。每個屬性對應資料庫 Works 表的一個欄位（Title 除外）。
/// </summary>
/// <remarks>
/// 型別決定欄位：string = NOT NULL、加 ? = 可為 NULL、[MaxLength(300)] = nvarchar(300)。
/// 改了這個類別，要跑 Migration 才會更新資料表（指令見 docs/commands.md）。
/// </remarks>
public class Work
{
    /// <summary>主鍵，資料庫自動編號。只是識別碼，不保證連續（失敗的新增、刪除都會留下空號）。</summary>
    public int Id { get; set; }

    /// <summary>
    /// 主要名稱：依中 → 日 → 英，取第一個有值的。
    /// 只有 get，所以不會變成資料庫欄位，每次讀取時才用 C# 算出來。
    /// 也因此不能用在 EF Core 的查詢（Where、OrderBy）裡：資料庫沒有這個欄位，翻譯不成 SQL。
    /// </summary>
    public string Title {
        get
        {
            if (!String.IsNullOrEmpty(TitleZh))
                return TitleZh;
            else if (!String.IsNullOrEmpty(TitleJa))
                return TitleJa;
            else if (!String.IsNullOrEmpty(TitleEn))
                return TitleEn;

            return "";
        }
    }

    // 沒填用空字串表示（不是 null）；資料庫的 CHECK 約束保證三個至少有一個不是空字串
    [MaxLength(300)]
    public string TitleZh { get; set; } = "";
    [MaxLength(300)]
    public string TitleJa { get; set; } = "";
    [MaxLength(300)]
    public string TitleEn { get; set; } = "";

    [MaxLength(300)]
    public string Remark { get; set; } = "";

    /// <summary>上架日。只有日期、沒有時區，所以用 DateOnly；可能不知道，所以可為 null。</summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>評分：0 = 未評價，1～5 = 星數，6 = 王冠。</summary>
    public int Score { get; set; }
    public bool Favorite { get; set; }

    /// <summary>已購買。暫時的欄位，之後會改成由各商店網址的購買狀態推導。</summary>
    public bool Purchased {get; set; }

    /// <summary>加入收藏的時間，一律存 UTC，顯示時再轉成當地時間。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>軟刪除的時間。有值 = 在「最近刪除」裡；null = 正常的作品。</summary>
    public DateTime? DeletedAt { get; set; }

}
