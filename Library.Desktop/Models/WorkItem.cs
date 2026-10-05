namespace Library.Desktop.Models;

// API 回傳的 JSON 對應到這個類別；只放畫面需要的欄位，其他欄位會被忽略
public record WorkItem(
    int Id,
    string Title,
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string Remark,
    int Score,
    bool Favorite,
    bool Purchased,
    DateOnly? ReleaseDate,
    DateTime CreatedAt);