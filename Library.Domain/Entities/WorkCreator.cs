namespace Library.Domain.Entities;

/// <summary>
/// 作品與創作者的多對多對應表，多帶一個 Role（所以寫成實際的類別，而不是讓 EF Core 自動產生）。
/// </summary>
/// <remarks>
/// 主鍵是 (WorkId, CreatorId, Role)：同一個人在同一部作品可以同時是作者和社團。
/// </remarks>
public class WorkCreator
{
    public int WorkId { get; set; }
    public Work Work { get; set; } = null!;

    public int CreatorId { get; set; }
    public Creator Creator { get; set; } = null!;

    public CreatorRole Role { get; set; }
}
