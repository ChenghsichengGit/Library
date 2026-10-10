namespace Library.Desktop.Models;

/// <summary>
/// 新增與修改時送給 API 的內容，欄位名稱要和後端的 SaveWorkRequest 對得上。
/// </summary>
public record SaveWorkRequest(
    string? TitleZh,
    string? TitleJa,
    string? TitleEn,
    string? Remark,
    DateOnly? ReleaseDate,
    int Score,
    bool Favorite,
    bool Purchased,
    // PUT 是整筆取代：沒送的話修改作品時作者和社團會被清空
    List<string> Authors,
    List<string> Circles,
    int WorkTypeId);
