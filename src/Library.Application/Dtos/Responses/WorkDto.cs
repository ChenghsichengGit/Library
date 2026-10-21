using Library.Application.Abstractions;
using Library.Domain.Entities;

namespace Library.Application.Dtos.Responses;

/// <summary>
/// API 回傳的作品格式。和 Work 分開，內部欄位（DeletedAt）不會外流，改資料表也不影響前端。
/// </summary>
public record WorkDto(
    int Id,
    string Title,
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string Description,
    DateOnly? ReleaseDate,
    int Score,
    bool Favorite,
    bool Purchased,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string[] Authors,
    string[] Circles,
    int WorkTypeId,
    string WorkType,
    string[] Tags,
    int? SeriesId,
    string? Series,
    WorkUrlDto[] Urls,
    WorkDlcDto[] Dlcs,
    string? CoverKey,
    string? CoverUrl)
{
    /// <summary>
    /// 從 Work 建立 WorkDto。查詢時要 Include Creators（含 Creator）、WorkType、Tags、Series 與 Urls、Dlcs。
    /// </summary>
    /// <remarks>封面同時回傳 key（修改時原樣送回，否則封面會被清空）和網址（顯示用，由 storage 組出）。</remarks>
    public static WorkDto From(Work w, ICoverStorage storage) =>
        new(
            w.Id,
            w.Title,
            w.TitleZh,
            w.TitleJa,
            w.TitleEn,
            w.Description,
            w.ReleaseDate,
            w.Score,
            w.Favorite,
            // 已購買不存欄位，由任一網址已購買推導
            w.Urls.Any(u => u.Purchased),
            w.CreatedAt,
            w.UpdatedAt,
            // 依名字排序：資料庫不保證回傳順序
            w.Creators.Where(wc => wc.Role == CreatorRole.Author).Select(wc => wc.Creator.Name).Order().ToArray(),
            w.Creators.Where(wc => wc.Role == CreatorRole.Circle).Select(wc => wc.Creator.Name).Order().ToArray(),
            w.WorkTypeId,
            w.WorkType.Name,
            w.Tags.Select(t => t.Name).Order().ToArray(),
            w.SeriesId,
            w.Series?.Name,
            w.Urls.OrderBy(u => u.Position)
                .Select(u =>
                    new WorkUrlDto(u.Url, u.Purchased, u.Language, u.Price, u.ListPrice, u.Discount, u.Currency))
                .ToArray(),
            w.Dlcs.OrderBy(d => d.Position)
                .Select(d => new WorkDlcDto(d.Name, d.Url, d.Purchased, d.Price, d.Currency))
                .ToArray(),
            string.IsNullOrEmpty(w.CoverKey) ? null : w.CoverKey,
            string.IsNullOrEmpty(w.CoverKey) ? null : storage.GetUrl(w.CoverKey)
        );
}
