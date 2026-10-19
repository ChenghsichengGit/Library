using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>
/// 作品清單的篩選與排序條件，對應網址的查詢參數，例如 /api/works?q=魔法&amp;minScore=3。
/// </summary>
/// <remarks>
/// 篩選條件都可為 null：沒給 = 不篩選，和 favorite=false（只要非最愛）意思不同。
/// 評分用範圍表示：三星以上 = minScore=3，未評價 = maxScore=0。
/// </remarks>
public class WorkQuery : IValidatableObject
{
    /// <summary>搜尋字：比對三種語言的名稱、備註、作者社團與系列名稱。</summary>
    public string? Q { get; set; }

    public bool? Favorite { get; set; }

    public bool? Purchased { get; set; }

    public int? WorkTypeId { get; set; }

    /// <summary>作者或社團的 Id，不分角色。</summary>
    public int? CreatorId { get; set; }

    public int? SeriesId { get; set; }

    [Range(0, 6)]
    public int? MinScore { get; set; }

    [Range(0, 6)]
    public int? MaxScore { get; set; }

    /// <summary>標籤名稱，網址寫成 ?tags=A&amp;tags=B。比對方式由 TagMode 決定。</summary>
    public List<string>? Tags { get; set; }

    [EnumDataType(typeof(TagMatch))]
    public TagMatch TagMode { get; set; } = TagMatch.All;

    // 只看還有 DLC 沒買的作品。只有 true 才篩選：「DLC 全買了」沒有用途，沒有 DLC 的作品也說不清算哪邊
    public bool DlcUnbought { get; set; }

    // EnumDataType 擋下 ?sort=99 這種轉得過去但沒定義的值
    [EnumDataType(typeof(WorkSort))]
    public WorkSort Sort { get; set; } = WorkSort.CreatedAt;

    /// <summary>true = 降冪；沒給就是升冪。</summary>
    public bool Desc { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 只給其中一個時，int? 的比較結果是 false，不會觸發
        if (MinScore > MaxScore)
        {
            yield return new ValidationResult("MinScore不能大於MaxScore", [nameof(MinScore), nameof(MaxScore)]);
        }
    }
}
