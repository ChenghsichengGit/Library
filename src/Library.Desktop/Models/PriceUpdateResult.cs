namespace Library.Desktop.Models;

/// <summary>對應 POST /api/prices/refresh 的回應。</summary>
public record PriceUpdateResult(int Updated, int NotUpdated);
