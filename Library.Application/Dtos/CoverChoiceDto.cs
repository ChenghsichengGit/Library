namespace Library.Application.Dtos;

/// <summary>作者／系列挑封面時的候選：底下某部作品的封面。</summary>
public record CoverChoiceDto(int WorkId, string Title, string CoverKey, string CoverUrl);
