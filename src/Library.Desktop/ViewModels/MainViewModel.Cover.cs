using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>表單的封面：選檔或貼上後縮圖上傳、從商店網址下載、移除。按儲存才會綁定到作品。</summary>
public partial class MainViewModel
{
    // 封面：key 存檔時送回（PUT 是整筆取代，沒送會被清空），網址給預覽
    [ObservableProperty] private string? _coverKey;

    // 完整網址，Image 直接繫結；null 時顯示「沒有封面」的提示
    [ObservableProperty] private string? _coverPreviewUrl;

    // 縮圖和上傳中：顯示進度圈；儲存要等上傳完成，否則存進去的是舊的 CoverKey
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isCoverBusy;

    // 例：「封面已上傳（850KB → 120KB）」
    [ObservableProperty] private string _coverStatus = "";

    /// <summary>「選擇圖片」：開啟檔案對話框，選了就上傳。</summary>
    [RelayCommand]
    private async Task PickCoverAsync()
    {
        var path = _dialog.PickImageFile();
        if (path is null)
            return;

        await UploadCoverAsync(await File.ReadAllBytesAsync(path));
    }

    /// <summary>縮圖後上傳，成功就換成新的封面（按儲存才會綁定到作品）。</summary>
    [RelayCommand]
    private async Task UploadCoverAsync(byte[] original)
    {
        IsCoverBusy = true;
        CoverStatus = "";
        try
        {
            // 縮圖是 CPU 運算，放到背景執行緒，畫面才不會卡住
            var content = await Task.Run(() => CoverImageShrinker.Shrink(original));
            var result = await _api.UploadCoverAsync(content, "cover");

            CoverKey = result.Key;
            CoverPreviewUrl = _api.ToAbsoluteUrl(result.Url);
            ErrorMessage = "";
            CoverStatus = content.Length < original.Length
                ? $"封面已上傳（{original.Length / 1024}KB → {content.Length / 1024}KB）"
                : "封面已上傳";
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "上傳失敗，請確認 Library.Api 有在執行";
        }
        finally
        {
            // 成功、失敗都要把進度圈關掉
            IsCoverBusy = false;
        }
    }

    /// <summary>請 API 從網址下載封面。失敗只顯示在封面狀態，不影響其他已抓到的資料。</summary>
    private async Task DownloadCoverAsync(string url)
    {
        IsCoverBusy = true;
        CoverStatus = "";
        try
        {
            var result = await _api.DownloadCoverAsync(url);
            CoverKey = result.Key;
            CoverPreviewUrl = _api.ToAbsoluteUrl(result.Url);
            CoverStatus = "已從商店帶入封面";
        }
        // 不寫進 ErrorMessage：抓取本身成功了，那一行紅字會讓人以為全部失敗
        catch (Exception ex) when (ex is ApiValidationException or HttpRequestException)
        {
            CoverStatus = "封面下載失敗，可以手動選擇圖片";
        }
        finally
        {
            IsCoverBusy = false;
        }
    }

    /// <summary>「移除封面」：只清掉表單，按儲存後後端才會刪除檔案。</summary>
    [RelayCommand]
    private void RemoveCover()
    {
        CoverKey = null;
        CoverPreviewUrl = null;
        CoverStatus = "";
    }
}
