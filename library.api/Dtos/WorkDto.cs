using library.api.Entities;

namespace library.api.Dtos;

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
    DateTime CreatedAt)
{
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
            w.CreatedAt);
}