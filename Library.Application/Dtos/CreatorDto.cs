namespace Library.Application.Dtos;

/// <summary>
/// 作者或社團清單的項目，WorkCount 是以這個角色參與、未刪除的作品數。
/// CoverKey 是自己設定的封面（沒設定為 null）；CoverUrl 沒設定時改用這個角色最新一部作品的封面。
/// </summary>
public record CreatorDto(
    int Id,
    string Name,
    int WorkCount,
    string Url,
    string Note,
    string? CoverKey,
    string? CoverUrl);
