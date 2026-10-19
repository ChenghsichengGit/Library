namespace Library.Application.Dtos.Requests;

/// <summary>作品清單的排序方式，對應網址的 ?sort=…（不分大小寫）。</summary>
/// <remarks>API 也接受數字，新值只加在最後，既有的數字才不會變。</remarks>
public enum WorkSort
{
    CreatedAt,
    ReleaseDate,
    Score,
    Title,
    UpdatedAt,
}