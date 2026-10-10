using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 創作者：作者、開發商、社團、發行商都在這張表，角色記在 WorkCreator.Role。
/// </summary>
/// <remarks>
/// 名字唯一（不分大小寫），新增作品時依名字沿用或建立。沒有作品的創作者不會自動刪除。
/// </remarks>
public class Creator
{
    public int Id { get; set; }

    [MaxLength(300)]
    public string Name { get; set; } = "";

    public List<WorkCreator> Works { get; set; } = [];
}
