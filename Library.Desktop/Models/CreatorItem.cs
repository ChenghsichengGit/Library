namespace Library.Desktop.Models;

/// <summary>對應 API 回傳的 CreatorDto。</summary>
public record CreatorItem(
    int Id,
    string Name,
    int WorkCount,
    string Url,
    string Note,
    string? CoverKey,
    string? CoverUrl);
