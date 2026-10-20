using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;

namespace Library.Desktop.ViewModels;

/// <summary>表單的重複提示：名稱、網址改了就在停頓後檢查；儲存前再檢查一次，完全重複要確認。</summary>
public partial class MainViewModel
{
    // 名稱改了就排程檢查
    partial void OnTitleZhChanged(string value) => ScheduleDuplicateCheck();
    partial void OnTitleJaChanged(string value) => ScheduleDuplicateCheck();
    partial void OnTitleEnChanged(string value) => ScheduleDuplicateCheck();

    private const int DuplicateCheckDelayMs = 350;

    // 每次排程都換一個新的；換掉時取消舊的，還在等或還在查的那次就不會更新畫面
    private CancellationTokenSource? _duplicateCheckCts;

    public ObservableCollection<DuplicateMatch> DuplicateMatches { get; } = [];

    // 提示框的標題，例如「⛔ 完全重複（1 筆）」；沒有重複時是 ""
    [ObservableProperty] private string _duplicateHeader = "";

    [ObservableProperty] private bool _isExactDuplicate;

    /// <summary>取消還沒完成的那次，重新等一段時間再檢查（debounce）。</summary>
    private void ScheduleDuplicateCheck()
    {
        _duplicateCheckCts?.Cancel();
        _duplicateCheckCts = new CancellationTokenSource();
        // 不 await：呼叫端（屬性的 setter）不用等；例外都在方法裡處理掉了
        _ = CheckDuplicatesAfterDelayAsync(_duplicateCheckCts.Token);
    }

    private async Task CheckDuplicatesAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(DuplicateCheckDelayMs, token);
            ShowDuplicates(await RequestDuplicateCheckAsync(token));
        }
        catch (OperationCanceledException)
        {
            // 又打字了，這次作廢
        }
        catch (HttpRequestException)
        {
            // 提示失敗不影響編輯；儲存時會再檢查一次
        }
    }

    /// <summary>用表單目前的內容問 API；名稱和網址都是空的就不問。</summary>
    private async Task<DuplicateCheckResult> RequestDuplicateCheckAsync(CancellationToken token = default)
    {
        var urls = Urls.Select(r => r.Url.Trim()).Where(u => u != "").ToList();
        if (TitleZh.Trim() == "" && TitleJa.Trim() == "" && TitleEn.Trim() == "" && urls.Count == 0)
            return new DuplicateCheckResult("none", []);

        return await _api.CheckDuplicatesAsync(TitleZh, TitleJa, TitleEn, Authors.GetNames(), urls, EditingId, token);
    }

    private void ShowDuplicates(DuplicateCheckResult result)
    {
        DuplicateMatches.Clear();
        foreach (var match in result.Matches)
            DuplicateMatches.Add(match);

        IsExactDuplicate = result.IsExact;
        DuplicateHeader = result.Matches.Length == 0 ? ""
            : result.IsExact ? $"⛔ 完全重複（{result.Matches.Length} 筆）"
            : $"⚠️ 可能重複（{result.Matches.Length} 筆）";
    }

    /// <summary>
    /// 儲存前用最新的內容再檢查一次。完全重複時問使用者，回傳 false 代表不要存。
    /// 伺服器不擋：同一部作品的不同版本也可能要收。
    /// </summary>
    private async Task<bool> ConfirmIfDuplicateAsync()
    {
        _duplicateCheckCts?.Cancel();
        var result = await RequestDuplicateCheckAsync();
        ShowDuplicates(result);
        if (!result.IsExact)
            return true;

        var first = result.Matches[0];
        return _dialog.Confirm($"和《{first.Title}》完全重複（{string.Join("、", first.Reasons)}），仍要儲存嗎？");
    }

    /// <summary>提示框的「看這筆」：把那部作品帶進表單；被篩選擋在清單外的也從 API 拿。</summary>
    [RelayCommand]
    private async Task ShowDuplicateAsync(int id)
    {
        try
        {
            SelectedWork = Works.FirstOrDefault(w => w.Id == id) ?? await _api.GetWorkAsync(id);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "讀取失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「掃描重複」：開啟全庫掃描視窗；關閉後重新讀取（可能刪了作品），選了某部就帶進表單。</summary>
    [RelayCommand]
    private async Task ScanDuplicatesAsync()
    {
        var selectedId = _dialog.ShowDuplicateScanner();

        await LoadTagsAsync();
        await LoadSeriesAsync();
        await LoadAsync();
        await LoadCreatorsAsync();

        if (selectedId is { } id)
            await ShowDuplicateAsync(id);
    }
}
