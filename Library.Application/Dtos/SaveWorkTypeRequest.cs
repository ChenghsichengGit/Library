using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>
/// 新增（POST）或修改（PUT）類型時前端送來的內容。
/// </summary>
public class SaveWorkTypeRequest
{
    [Required]
    [MaxLength(32)]
    public string? Name { get; set; }

    /// <summary>顯示順序。新增時沒給就排在「其他」前面；修改時沒給就保留原本的。</summary>
    [Range(0, 999)]
    public int? SortOrder { get; set; }
}
