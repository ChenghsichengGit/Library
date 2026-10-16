using CommunityToolkit.Mvvm.ComponentModel;
using Library.Desktop.Models;

namespace Library.Desktop.ViewModels;

/// <summary>表單 DLC 清單的一列。</summary>
public partial class DlcRow : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private bool _purchased;
    // 用字串接：清空輸入框時 decimal? 會轉換失敗而保留舊值
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private string _currency = "";

    /// <summary>把 API 讀到的 DLC 轉成一列。</summary>
    public static DlcRow From(WorkDlcItem item) => new()
    {
        Name = item.Name,
        Url = item.Url,
        Purchased = item.Purchased,
        PriceText = item.Price?.ToString() ?? "",
        Currency = item.Currency
    };

    /// <summary>轉成要送給 API 的格式。</summary>
    public WorkDlcItem ToItem() => new(
        Name,
        Url,
        Purchased,
        decimal.TryParse(PriceText, out var price) ? price : null,
        Currency);
}
