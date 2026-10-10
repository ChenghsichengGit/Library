using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 作品類型（漫畫、動畫……），可自訂。名稱唯一；還有作品使用的類型不能刪除。
/// </summary>
public class WorkType
{
    public int Id { get; set; }

    [MaxLength(32)]
    public string Name { get; set; } = "";

    /// <summary>清單與下拉選單的顯示順序，小的在前。</summary>
    public int SortOrder { get; set; }

    public List<Work> Works { get; set; } = [];
}
