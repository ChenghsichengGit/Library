using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 全庫重複掃描視窗：列出互相重複的作品，可以刪除或選一部帶回主視窗。
/// </summary>
public partial class DuplicatesViewModel(WorksApiClient api, IDialogService dialog) : ObservableObject
{
    public ObservableCollection<DuplicateGroup> Groups { get; } = [];

    [ObservableProperty] private string _statusMessage = "";

    /// <summary>按了「看這筆」的作品；視窗關閉後由主視窗帶進表單。沒有選就是 null。</summary>
    public int? SelectedWorkId { get; private set; }

    /// <summary>ViewModel 不認識視窗，要關閉時發出這個事件，由視窗自己 Close()。</summary>
    public event Action? CloseRequested;

    /// <summary>掃描並列出重複的分組。視窗開啟時與刪除後執行。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = "掃描中…";
        try
        {
            var groups = await api.GetDuplicateGroupsAsync();
            Groups.Clear();
            foreach (var group in groups)
                Groups.Add(group);
            StatusMessage = groups.Count == 0 ? "沒有發現重複" : $"找到 {groups.Count} 組重複";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「刪除」：確認後刪除（軟刪除），再重新掃描。</summary>
    [RelayCommand]
    private async Task DeleteAsync(DuplicateGroupItem item)
    {
        if (!dialog.Confirm($"把《{item.Title}》移到最近刪除？之後可以還原。"))
            return;

        try
        {
            await api.DeleteAsync(item.Id);
            await LoadAsync();
        }
        catch (HttpRequestException)
        {
            StatusMessage = "刪除失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「看這筆」：記下 Id 並關閉視窗，主視窗會把它帶進表單。</summary>
    [RelayCommand]
    private void Show(DuplicateGroupItem item)
    {
        SelectedWorkId = item.Id;
        CloseRequested?.Invoke();
    }
}
