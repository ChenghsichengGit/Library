using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 創作者：作者、開發商、社團、發行商都存在這張表，角色由 WorkCreator.Role 區分。
/// </summary>
/// <remarks>
/// 名字有唯一索引（不分大小寫），同一個名字只會有一筆；新增作品時由 WorkService 依名字沿用或建立。
/// 沒有任何作品連到的創作者會留在表裡，不會自動刪除。
/// </remarks>
public class Creator
{
    public int Id { get; set; }

    [MaxLength(300)]
    public string Name { get; set; } = "";

    /// <summary>反向導覽：這個人參與的所有作品（經過 WorkCreator）。之後做「點作者名稱列出作品」時使用。</summary>
    public List<WorkCreator> Works { get; set; } = [];
}
