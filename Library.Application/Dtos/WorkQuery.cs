using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

// 作品清單的篩選條件，對應網址上的查詢參數；沒給的條件就不篩選，所以全部可為 null
public class WorkQuery :  IValidatableObject
{
    public string? Q { get; set; }

    public bool? Favorite { get; set; }

    public bool? Purchased { get; set; }

    [Range(0, 6)]
    public int? MinScore { get; set; }
    [Range(0, 6)]
    public int? MaxScore { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinScore > MaxScore)
        {
            yield return new ValidationResult("MinScore不能大於MaxScore", [nameof(MinScore), nameof(MaxScore)]);
        }
    }
}