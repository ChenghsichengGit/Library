using System.Globalization;

namespace Library.Desktop.Models;

/// <summary>
/// 價格的顯示文字：幣別換成符號，台幣、日圓、韓圜不顯示小數（同原版）。
/// </summary>
public static class PriceFormat
{
    private static readonly Dictionary<string, string> Signs = new()
    {
        ["JPY"] = "¥", ["TWD"] = "NT$", ["USD"] = "$", ["EUR"] = "€", ["CNY"] = "¥", ["KRW"] = "₩", ["GBP"] = "£"
    };

    private static readonly HashSet<string> NoDecimal = ["JPY", "TWD", "KRW"];

    /// <summary>例如「NT$134」「$9.99」；不認得的幣別直接寫代碼，例如「HKD 50.00」。</summary>
    public static string Format(decimal price, string currency)
    {
        var code = currency.Trim().ToUpperInvariant();
        // 千分位用固定的格式，不跟著電腦語系變
        var number = NoDecimal.Contains(code)
            ? Math.Round(price).ToString("N0", CultureInfo.InvariantCulture)
            : price.ToString("N2", CultureInfo.InvariantCulture);
        var prefix = Signs.TryGetValue(code, out var sign) ? sign : code == "" ? "" : code + " ";
        return prefix + number;
    }

    /// <summary>有特價時是「NT$268 → NT$134（−50%）」，沒有時只有價格。</summary>
    public static string WithDiscount(decimal price, string currency, decimal? listPrice, int discount) =>
        discount > 0 && listPrice is { } list
            ? $"{Format(list, currency)} → {Format(price, currency)}（−{discount}%）"
            : Format(price, currency);
}
