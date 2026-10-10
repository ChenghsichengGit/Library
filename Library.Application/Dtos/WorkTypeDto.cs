namespace Library.Application.Dtos;

/// <summary>
/// API 回傳的作品類型。不含 Works，避免序列化時循環參照。
/// </summary>
public record WorkTypeDto(int Id, string Name, int SortOrder);
