namespace Library.Application.Dtos.Responses;

/// <summary>和既有作品重複的一筆；Reasons 例如「網址相同」「作品名相同」。</summary>
public record DuplicateMatchDto(int Id, string Title, string[] Authors, DuplicateLevel Level, string[] Reasons);

/// <summary>重複檢查的結果：Level 是所有 Matches 中最嚴重的，沒有重複時是 None。</summary>
public record DuplicateCheckDto(DuplicateLevel Level, DuplicateMatchDto[] Matches);
