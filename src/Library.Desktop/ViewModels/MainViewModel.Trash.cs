using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Library.Desktop.ViewModels;

/// <summary>最近刪除：工具列按鈕的數量，以及開啟最近刪除視窗。</summary>
public partial class MainViewModel
{
    // 工具列按鈕的文字，例如「最近刪除 (3)」；沒有刪除的作品時不顯示數字
    [ObservableProperty] private string _trashButtonText = "最近刪除";

    /// <summary>讀取最近刪除的數量，更新按鈕文字。由 LoadAsync 呼叫。</summary>
    private async Task LoadTrashCountAsync()
    {
        try
        {
            var count = (await _api.GetTrashAsync()).Items.Length;
            TrashButtonText = count == 0 ? "最近刪除" : $"最近刪除 ({count})";
        }
        catch (HttpRequestException)
        {
            // 數量只是提示，讀不到不影響其他功能
        }
    }

    /// <summary>「最近刪除」：開啟視窗，關閉後重新讀取（可能還原或永久刪除了作品）。</summary>
    [RelayCommand]
    private async Task OpenTrashAsync()
    {
        _dialog.ShowTrash();

        // 還原的作品可能帶回標籤、系列、作者，篩選選單要更新
        await LoadTagsAsync();
        await LoadSeriesAsync();
        await LoadAsync();
        await LoadCreatorsAsync();
    }
}
