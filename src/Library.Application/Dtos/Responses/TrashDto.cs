namespace Library.Application.Dtos.Responses;

/// <summary>最近刪除的一部作品；沒有封面時 CoverUrl 是 null。</summary>
public record TrashItemDto(
    int Id,
    string Title,
    string WorkType,
    string[] Authors,
    string? CoverUrl,
    DateTime DeletedAt);

/// <summary>最近刪除的清單；刪除超過 KeepDays 天的作品，會在 API 啟動時永久清除。</summary>
public record TrashDto(int KeepDays, TrashItemDto[] Items);
