using Library.Domain.Entities;

namespace Library.Application.Dtos;

/// <summary>
/// API 回傳給前端的作品格式（對外的合約）。
/// </summary>
/// <remarks>
/// 不直接回傳 Work（資料庫的格式）的原因：
/// ① 只有放在這裡的欄位會送出去，DeletedAt 這種內部欄位不會外流；
/// ② 資料表改名或加欄位時，只要這裡不變，前端（WPF）就不受影響；
/// ③ 之後加了作者等關聯，直接序列化 Work 會無限循環。
/// record：建立後內容就固定，像一個打包好的封包。
/// </remarks>
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
    DateTime CreatedAt)
{
    /// <summary>
    /// 從 Work 建立 WorkDto。WorkService 用 list.Select(WorkDto.From) 一筆一筆轉換。
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
            w.CreatedAt);
}
