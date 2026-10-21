using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Services;
using Library.Desktop.ViewModels.Parts;

namespace Library.Desktop.ViewModels;

/// <summary>貼上商店網址按「抓取」：查詢後填進表單，不存檔。</summary>
public partial class MainViewModel
{
    // 只用來抓取資料填表單，不會存進作品
    [ObservableProperty] private string _storeUrl = "";

    /// <summary>
    /// 「抓取」：依商店網址查詢，把資料填進表單，不存檔。
    /// </summary>
    /// <remarks>
    /// 有值才覆蓋：商店沒有的欄位保留使用者已填的內容；標籤只加入、簡介空的才填，評分、最愛不碰。商店有封面時請 API 下載並換上。
    /// 不改 EditingId：新增模式下抓取就是新增，編輯中抓取就是更新那部作品。
    /// </remarks>
    [RelayCommand]
    private async Task FetchAsync()
    {
        // 空白送出去會收到一大段英文的驗證錯誤
        if (string.IsNullOrWhiteSpace(StoreUrl))
        {
            ErrorMessage = "請輸入網址";
            return;
        }

        try
        {
            var info = await _api.LookupAsync(StoreUrl.Trim());
            TitleZh = KeepIfEmpty(info.TitleZh, TitleZh);
            TitleJa = KeepIfEmpty(info.TitleJa, TitleJa);
            TitleEn = KeepIfEmpty(info.TitleEn, TitleEn);
            if (info.Authors.Length > 0)
                Authors.SetNames(info.Authors);
            if (info.Circles.Length > 0)
                Circles.SetNames(info.Circles);

            Tags.AddNames(info.Tags);
            // 簡介只在表單是空的時候填，不蓋掉自己寫的
            if (string.IsNullOrWhiteSpace(Description))
                Description = info.Description;

            ReleaseDate = info.ReleaseDate?.ToDateTime(TimeOnly.MinValue) ?? ReleaseDate;
            // 抓取用的網址就是這部作品的網址：清單裡已經有就沿用那一列，沒有才新增；有價格就填進那一列
            var url = StoreUrl.Trim();
            var row = Urls.FirstOrDefault(r => r.Url.Trim().Equals(url, StringComparison.OrdinalIgnoreCase));

            if (row == null)
            {
                row = new UrlRow { Url = url };
                Urls.Add(row);
            }

            if (info.Price is { } price)
            {
                row.PriceText = price.ToString();
                row.ListPrice = info.ListPrice;
                row.Discount = info.Discount;
                row.Currency = info.Currency;
            }

            var type = WorkTypes.FirstOrDefault(w => w.Name == info.WorkType);
            if (type != null)
                WorkTypeId = type.Id;

            ErrorMessage = "";

            // 其他欄位已經填好了，封面另外下載，失敗也不影響
            if (info.CoverImageUrl is { } coverUrl)
                await DownloadCoverAsync(coverUrl);
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "查詢失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「更新價格」：重新查詢所有 Steam、Microsoft Store 網址的價格，完成後重新讀取清單。</summary>
    [RelayCommand]
    private async Task RefreshPricesAsync()
    {
        StatusMessage = "更新價格中…";
        try
        {
            var result = await _api.RefreshPricesAsync();
            await LoadAsync();

            // 編輯中的作品重新帶入表單，網址那一列才會顯示新的價格
            if (EditingId is { } id)
                SelectedWork = Works.FirstOrDefault(w => w.Id == id);

            // LoadAsync 會把狀態改成「共 N 部作品」，所以結果要在最後才設定
            StatusMessage = $"已更新 {result.Updated} 個網址的價格"
                            + (result.NotUpdated > 0 ? $"，{result.NotUpdated} 個沒有更新（免費、下架或連線失敗）" : "");
        }
        catch (HttpRequestException)
        {
            StatusMessage = "更新價格失敗，請確認 Library.Api 有在執行";
        }
    }

    private static string KeepIfEmpty(string fetched, string current) =>
        fetched == "" ? current : fetched;
}
