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
    [ObservableProperty] private string _currency = "";

    /// <summary>把 API 讀到的網址轉成一列。</summary>
    public static UrlRow From(WorkUrlItem item) => new()
    {
        Url = item.Url,
        Purchased = item.Purchased,
        Language = item.Language ?? "",
        PriceText = item.Price?.ToString() ?? "",
        Currency = item.Currency
    };

    /// <summary>轉成要送給 API 的格式。</summary>
    public WorkUrlItem ToItem() => new(
        Url,
        Purchased,
        Language == "" ? null : Language,
        decimal.TryParse(PriceText, out var price) ? price : null,
        Currency);
}
