using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>
/// 前端新增（POST）或修改（PUT）作品時送來的內容。
/// </summary>
/// <remarks>
/// 沒有 Id、CreatedAt：這些由系統決定，前端就算送了也會被忽略（防止偷改，叫 over-posting）。
/// 欄位都可為 null：前端可能沒送某個欄位。如果宣告成 string，ASP.NET Core 會把它當成必填。
/// 驗證由 [ApiController] 在進 Controller 之前自動執行，不通過直接回 400。
/// </remarks>
public class SaveWorkRequest : IValidatableObject
{
    // 單一欄位的規則用標記寫在屬性上
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
    public bool? Purchased {get; set; }

    // 作者、社團只送名字，Id 和是否已存在由 WorkService 處理；空白和重複的名字也由它濾掉，不算錯誤
    // 不加 [MaxLength]：加在 List 上限制的是項目數，不是每個名字的長度，所以長度在 Validate 裡檢查
    public List<string>? Authors { get; set; }
    public List<string>? Circles { get; set; }

    /// <summary>
    /// 跨欄位的規則（標記做不到的）寫在這裡。每個 yield return 是一個錯誤，全部會一起回傳給前端。
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 用 IsNullOrWhiteSpace 而不是 IsNullOrEmpty：只有空白的 "   " 也要當成沒填
        if (String.IsNullOrWhiteSpace(TitleZh) && String.IsNullOrWhiteSpace(TitleJa) && String.IsNullOrWhiteSpace(TitleEn))
        {
            // 第二個參數是「錯在哪些欄位」，前端可以用來把錯誤標在對應的輸入框旁
            yield return new ValidationResult(
                "至少輸入一個名稱", [nameof(TitleZh), nameof(TitleJa), nameof(TitleEn)]);
        }

        if (Score is < 0 or > 6)
        {
            yield return new ValidationResult(
                "評分錯誤", [nameof(Score)]);
        }

        IEnumerable<string> list = (Authors ?? []).Concat(Circles ?? []);

        // n?.Length：JSON 可能送來 ["A", null]，型別宣告擋不住；null 會被跳過，之後由 WorkService 濾掉
        if (list.Any(n => n?.Length > 300))
        {
            yield return new ValidationResult(
                "作者或社團名稱不能超過 300 字", [nameof(Authors), nameof(Circles)]);
        }
    }
}
