using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>修改系列時前端送來的內容。備註、封面沒填時送 null。</summary>
public class SaveSeriesRequest
{
    [Required]
    [MaxLength(300)]
    public string? Name { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [MaxLength(100)]
    [RegularExpression(@"^[0-9a-f]{32}\.(jpg|png|gif|webp)$")]
    public string? CoverKey { get; set; }
}
