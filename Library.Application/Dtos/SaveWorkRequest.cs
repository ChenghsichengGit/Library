using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

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
    public bool? Purchased {get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (String.IsNullOrWhiteSpace(TitleZh) && String.IsNullOrWhiteSpace(TitleJa) && String.IsNullOrWhiteSpace(TitleEn))
        {
            yield return new ValidationResult(
                "至少輸入一個名稱", [nameof(TitleZh), nameof(TitleJa), nameof(TitleEn)]);
        }

        if (Score is < 0 or > 6)
        {
            yield return new ValidationResult(
                "評分錯誤", [nameof(Score)]);
        }
    }
}