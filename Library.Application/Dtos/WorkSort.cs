namespace Library.Application.Dtos;

/// <summary>作品清單的排序方式，對應網址的 ?sort=…（不分大小寫）。</summary>
public enum WorkSort
{
    CreatedAt,
    ReleaseDate,
    Score,
    Title,
}