namespace Library.Desktop.Models;

/// <summary>
/// 作品清單的查詢條件，由 WorksApiClient 組成網址上的 ?q=…&amp;sort=…。
/// 對應後端的 WorkQuery；可為 null 的條件代表「不篩選」。
/// </summary>
public record WorkListQuery(
    string? Q,
    bool? Favorite,
    int? MinScore,
    int? MaxScore,
    int? WorkTypeId,
    int? CreatorId,
    int? SeriesId,
    bool DlcUnbought,
    bool OnSale,
    List<string> Tags,
    bool TagAny,
    string Sort,
    bool Desc);
