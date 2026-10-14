namespace Library.Application.Dtos;

/// <summary>系列清單的項目。Authors 由系列底下未刪除作品的作者推算。</summary>
public record SeriesDto(int Id, string Name, int WorkCount, string[] Authors);