namespace Library.Application.Dtos;

/// <summary>
/// 系列清單的項目。Authors 由系列底下未刪除作品的作者推算。
/// CoverKey 是自己設定的封面（沒設定為 null）；CoverUrl 沒設定時改用系列裡最新一部作品的封面。
/// </summary>
public record SeriesDto(
    int Id,
    string Name,
    int WorkCount,
    string[] Authors,
    string Note,
    string? CoverKey,
    string? CoverUrl);
