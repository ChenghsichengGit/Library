namespace Library.Desktop.Models;

// 新增與修改時送給 API 的內容；欄位名稱要和後端的 SaveWorkRequest 對得上
public record SaveWorkRequest(
    string? TitleZh,
    string? TitleJa,
    string? TitleEn,
    string? Remark,
    DateOnly? ReleaseDate,
    int Score,
    bool Favorite,
    bool Purchased);