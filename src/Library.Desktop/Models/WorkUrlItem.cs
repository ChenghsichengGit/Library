namespace Library.Desktop.Models;

/// <summary>作品的一個網址，對應 API 的 WorkUrlDto；送出時也用同一個格式。</summary>
public record WorkUrlItem(
    string Url,
    bool Purchased,
    string? Language,
    decimal? Price,
    decimal? ListPrice,
    int Discount,
    string Currency);
