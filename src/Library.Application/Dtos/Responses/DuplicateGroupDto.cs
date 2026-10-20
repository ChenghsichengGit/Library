namespace Library.Application.Dtos.Responses;

/// <summary>全庫掃描的一組互相重複的作品；Level、Reasons 是這組裡最嚴重的等級和所有理由。</summary>
public record DuplicateGroupDto(DuplicateLevel Level, string[] Reasons, DuplicateGroupItemDto[] Items);

/// <summary>重複組裡的一部作品。</summary>
public record DuplicateGroupItemDto(int Id, string Title, string[] Authors);
