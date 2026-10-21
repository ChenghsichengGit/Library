using CommunityToolkit.Mvvm.ComponentModel;
using Library.Desktop.Models;

namespace Library.Desktop.ViewModels.Parts;

/// <summary>表單網址清單的一列。</summary>
public partial class UrlRow : ObservableObject
{
    [ObservableProperty] private string _url = "";

    [ObservableProperty] private bool _purchased;

    // "" 代表未指定：ComboBox 的 SelectedValue 是 null 時不會選中任何選項
    [ObservableProperty] private string _language = "";

    // 用字串接：清空輸入框時 decimal? 會轉換失敗而保留舊值
    [ObservableProperty] private string _priceText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountText))]
    private string _currency = "";

    // 抓取時帶回的原價與折扣，只代表抓取當下；手動改價格時清掉（OnPriceTextChanged）
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountText))]
    private decimal? _listPrice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountText))]
    private int _discount;

    /// <summary>價格旁的灰字，例如「原價 NT$268（−50%）」；沒有特價時是 ""。</summary>
    public string DiscountText => Discount > 0 && ListPrice is { } list
        ? $"原價 {PriceFormat.Format(list, Currency)}（−{Discount}%）"
        : "";

    /// <summary>把 API 讀到的網址轉成一列。</summary>
    public static UrlRow From(WorkUrlItem item) => new()
    {
        Url = item.Url,
        Purchased = item.Purchased,
        Language = item.Language ?? "",
        PriceText = item.Price?.ToString() ?? "",
        ListPrice = item.ListPrice,
        Discount = item.Discount,
        Currency = item.Currency
    };

    /// <summary>轉成要送給 API 的格式。</summary>
    public WorkUrlItem ToItem() => new(
        Url,
        Purchased,
        Language == "" ? null : Language,
        decimal.TryParse(PriceText, out var price) ? price : null,
        ListPrice,
        Discount,
        Currency);

    // 設定順序要先價格、再原價與折扣（From、抓取都是），否則剛設好的原價會被這裡清掉
    partial void OnPriceTextChanged(string value)
    {
        ListPrice = null;
        Discount = 0;
    }
}
