namespace Library.Desktop.Models;

/// <summary>
/// 新增與修改時送給 API 的內容，會被轉成 JSON 放在 request body 裡。
/// 欄位名稱要和後端的 SaveWorkRequest 對得上。
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
    // PUT 是整筆取代：這兩個沒送的話，修改作品時作者和社團會被清空
    List<string> Authors,
    List<string> Circles,
    int WorkTypeId
);