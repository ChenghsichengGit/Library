using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 作品標籤，和作品多對多。名稱唯一（不分大小寫）。
/// </summary>
public class Tag
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = "";

    public List<Work> Works { get; set; } = [];
}