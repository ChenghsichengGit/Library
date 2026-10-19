using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 作品類型（漫畫、動畫……），可自訂。名稱唯一。
/// </summary>
public class WorkType
{
    /// <summary>「其他」的 Id（種子資料）：沒有合適類型時使用，不能刪除；刪除其他類型時作品會改成它。</summary>
    public const int OtherId = 6;

    public int Id { get; set; }

    [MaxLength(32)]
    public string Name { get; set; } = "";

    /// <summary>清單與下拉選單的顯示順序，小的在前。</summary>
    public int SortOrder { get; set; }

    public List<Work> Works { get; set; } = [];
}
