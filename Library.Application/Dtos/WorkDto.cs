using Library.Domain.Entities;

namespace Library.Application.Dtos;

/// <summary>
/// API 回傳的作品格式。和 Work 分開，內部欄位（DeletedAt）不會外流，改資料表也不影響前端。
/// </summary>
public record WorkDto(
    int Id,
    string Title,
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string Remark,
    DateOnly? ReleaseDate,
    int Score,
    bool Favorite,
    bool Purchased,
    DateTime CreatedAt,
    string[] Authors,
    string[] Circles,
    int WorkTypeId,
    string WorkType)
{
    /// <summary>
    /// 從 Work 建立 WorkDto。查詢時要 Include Creators（含 Creator）與 WorkType。
    /// </summary>
    public static WorkDto From(Work w) =>
        new(
            w.Id,
            w.Title,
            w.TitleZh,
            w.TitleJa,
            w.TitleEn,
            w.Remark,
            w.ReleaseDate,
            w.Score,
            w.Favorite,
            w.Purchased,
            w.CreatedAt,
            // 依名字排序：資料庫不保證回傳順序
            w.Creators.Where(wc => wc.Role == CreatorRole.Author).Select(wc => wc.Creator.Name).Order().ToArray(),
            w.Creators.Where(wc => wc.Role == CreatorRole.Circle).Select(wc => wc.Creator.Name).Order().ToArray(),
            w.WorkTypeId,
            w.WorkType.Name);
}
