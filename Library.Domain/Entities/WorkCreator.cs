namespace Library.Domain.Entities;

/// <summary>
/// 作品與創作者的多對多對應表，多帶一個 Role 欄位（帶資料的對應表）。
/// </summary>
/// <remarks>
/// EF Core 能自動產生只有兩個 Id 的隱藏對應表，但我們需要 Role，所以寫成實際的類別。
/// 主鍵是 (WorkId, CreatorId, Role)：同一個人在同一部作品可以同時是作者和社團，但同一個角色不會重複。
/// Id 和物件成對出現：Id 是資料庫實際存的欄位；物件是導覽屬性，要 Include 才有值。
/// 新增時只要設定物件，存檔時 EF Core 會依物件填入 Id（新物件存檔前還沒有 Id）。
/// </remarks>
public class WorkCreator
{
    public int WorkId { get; set; }

    // = null!：沒 Include 時確實是 null，但 EF Core 讀取時會填入，這裡只是告訴編譯器不要警告
    public Work Work { get; set; } = null!;

    public int CreatorId { get; set; }
    public Creator Creator { get; set; } = null!;
    public CreatorRole Role { get; set; }
}
