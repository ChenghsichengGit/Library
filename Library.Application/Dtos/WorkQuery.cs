using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>
/// 作品清單的篩選條件，對應網址上的查詢參數，例如 /api/works?q=魔法&amp;minScore=3。
/// </summary>
/// <remarks>
/// 全部可為 null，因為「沒給」和「給了 false」意思不同：
/// favorite 沒給 = 不篩選；favorite=false = 只要不是最愛的。
/// 評分用 MinScore／MaxScore 表示範圍：三星以上 = minScore=3，未評價 = maxScore=0。
/// </remarks>
public class WorkQuery :  IValidatableObject
{
    /// <summary>搜尋字：比對三種語言的名稱與備註。</summary>
    public string? Q { get; set; }

    public bool? Favorite { get; set; }

    public bool? Purchased { get; set; }

    [Range(0, 6)]
    public int? MinScore { get; set; }
    [Range(0, 6)]
    public int? MaxScore { get; set; }

    /// <summary>排序方式，?sort=title 會自動轉成 WorkSort.Title。排序一定有值（沒給就依加入時間），所以不可為 null。</summary>
    // EnumDataType：enum 底層是整數，?sort=99 也轉得過去；加了它才會擋下沒定義的值，回 400
    [EnumDataType(typeof(WorkSort))]
    public WorkSort Sort { get; set; } = WorkSort.CreatedAt;

    /// <summary>true = 降冪（大到小、新到舊、Z 到 A）；沒給就是升冪。</summary>
    public bool Desc { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // int? 比較時只要有一邊是 null，結果就是 false，所以只給其中一個時不會觸發
        if (MinScore > MaxScore)
        {
            yield return new ValidationResult("MinScore不能大於MaxScore", [nameof(MinScore), nameof(MaxScore)]);
        }
    }
}
