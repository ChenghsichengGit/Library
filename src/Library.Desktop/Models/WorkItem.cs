namespace Library.Desktop.Models;

/// <summary>
/// 前端的作品資料，對應 API 回傳的 WorkDto。WPF 不引用後端的程式碼，只照 JSON 的格式定義。
/// </summary>
public record WorkItem(
    int Id,
    string Title,
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string Description,
    int Score,
    bool Favorite,
    bool Purchased,
    DateOnly? ReleaseDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string[] Authors,
    string[] Circles,
    int WorkTypeId,
    string WorkType,
    string[] Tags,
    int? SeriesId,
    string? Series,
    WorkUrlItem[] Urls,
    WorkDlcItem[] Dlcs,
    string? CoverKey,
    string? CoverUrl)
{
    // 以下只給畫面顯示用

    /// <summary>加入時間的當地時間（API 回傳 UTC）。</summary>
    public DateTime CreatedAtLocal => CreatedAt.ToLocalTime();

    /// <summary>修改時間的當地時間。</summary>
    public DateTime UpdatedAtLocal => UpdatedAt.ToLocalTime();

    /// <summary>評分的顯示文字：0 不顯示、1～5 星星、6 王冠。</summary>
    public string ScoreText => Score switch
    {
        0 => "",
        6 => "👑",
        _ => new string('★', Score)
    };

    // 表格只標出「是」的作品，True／False 整欄都是字，看不出哪些有勾
    public string FavoriteText => Favorite ? "♥" : "";

    public string PurchasedText => Purchased ? "✓" : "";

    /// <summary>
    /// 表格的價格欄：顯示目前最便宜的網址，價格相同時取折扣大的。
    /// 不同幣別無法直接比較，有台幣價格（Steam、Microsoft Store）時只在台幣之間比。
    /// </summary>
    public string PriceText
    {
        get
        {
            var priced = Urls.Where(u => u.Price is not null).ToList();
            if (priced.Count == 0)
                return "";

            var twd = priced.Where(u => u.Currency == "TWD").ToList();
            var shown = (twd.Count > 0 ? twd : priced)
                .OrderBy(u => u.Price)
                .ThenByDescending(u => u.Discount)
                .First();
            return PriceFormat.WithDiscount(shown.Price!.Value, shown.Currency, shown.ListPrice, shown.Discount);
        }
    }

    /// <summary>表格用「、」接成一行。</summary>
    public string AuthorsText => string.Join("、", Authors);

    public string CirclesText => string.Join("、", Circles);

    public string TagsText => string.Join("、", Tags);
}
