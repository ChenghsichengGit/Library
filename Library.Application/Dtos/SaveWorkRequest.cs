using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>
/// 新增（POST）或修改（PUT）作品時前端送來的內容。
/// </summary>
/// <remarks>
/// 不含 Id、CreatedAt，前端無法指定（防止 over-posting）。
/// 欄位都可為 null，沒送的欄位由 WorkService 給預設值；必填的用 [Required] 標記。
/// </remarks>
public class SaveWorkRequest : IValidatableObject
{
    [MaxLength(300)]
    public string? TitleZh { get; set; }

    [MaxLength(300)]
    public string? TitleJa { get; set; }

    [MaxLength(300)]
    public string? TitleEn { get; set; }

    [MaxLength(300)]
    public string? Remark { get; set; } = "";

    public DateOnly? ReleaseDate { get; set; }
    public int? Score { get; set; }
    public bool? Favorite { get; set; }

    // 只送名字，空白與重複由 WorkService 濾掉；[MaxLength] 加在 List 上限制的是項目數，所以名字長度在 Validate 檢查
    public List<string>? Authors { get; set; }
    public List<string>? Circles { get; set; }

    // int? 加 [Required]：沒送時是 null 才擋得下來，宣告成 int 會變成 0 而通過
    [Required]
    public int? WorkTypeId { get; set; }

    // 整理方式同作者；名稱上限 100，在 Validate 檢查
    public List<string>? Tags { get; set; }

    // 系列名稱，找不到就新建；空白代表沒有系列
    [MaxLength(300)]
    public string? Series { get; set; }

    // 整份取代作品的網址；空白列與正規化後重複的由 WorkService 濾掉
    public List<SaveWorkUrlRequest>? Urls { get; set; }

    public List<SaveWorkDlcRequest>? Dlcs { get; set; }

    /// <summary>跨欄位的驗證規則，屬性標記都通過後才會執行。</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (String.IsNullOrWhiteSpace(TitleZh) && String.IsNullOrWhiteSpace(TitleJa) &&
            String.IsNullOrWhiteSpace(TitleEn))
        {
            yield return new ValidationResult(
                "至少輸入一個名稱", [nameof(TitleZh), nameof(TitleJa), nameof(TitleEn)]);
        }

        if (Score is < 0 or > 6)
        {
            yield return new ValidationResult(
                "評分錯誤", [nameof(Score)]);
        }

        IEnumerable<string> list = (Authors ?? []).Concat(Circles ?? []);

        // JSON 可能送來 ["A", null]，所以用 ?.
        if (list.Any(n => n?.Length > 300))
        {
            yield return new ValidationResult(
                "作者或社團名稱不能超過 300 字", [nameof(Authors), nameof(Circles)]);
        }

        if ((Tags ?? []).Any(t => t?.Length > 100))
        {
            yield return new ValidationResult("標籤名稱不能超過 100 字", [nameof(Tags)]);
        }
    }
}