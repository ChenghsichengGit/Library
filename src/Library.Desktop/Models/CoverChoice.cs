namespace Library.Desktop.Models;

/// <summary>對應 API 的 CoverChoiceDto：可以挑來當封面的作品封面。</summary>
public record CoverChoice(int WorkId, string Title, string CoverKey, string CoverUrl);
