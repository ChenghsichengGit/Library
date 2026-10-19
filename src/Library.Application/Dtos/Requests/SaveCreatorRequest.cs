using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos.Requests;

/// <summary>修改作者／社團時前端送來的內容。網址、備註、封面沒填時送 null。</summary>
public class SaveCreatorRequest
{
    [Required]
    [MaxLength(300)]
    public string? Name { get; set; }

    // [Url] 只接受 http、https、ftp 開頭；null 會直接通過
    [MaxLength(2000)]
    [Url]
    public string? Url { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [MaxLength(100)]
    [RegularExpression(@"^[0-9a-f]{32}\.(jpg|png|gif|webp)$")]
    public string? CoverKey { get; set; }
}
