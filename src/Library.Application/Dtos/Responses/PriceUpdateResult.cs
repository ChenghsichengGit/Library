namespace Library.Application.Dtos.Responses;

/// <summary>更新價格的結果：Updated 是更新了幾個網址，NotUpdated 是查不到價格的（免費、下架或連線失敗）。</summary>
public record PriceUpdateResult(int Updated, int NotUpdated);
