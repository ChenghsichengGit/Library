using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 最近刪除視窗：列出已刪除的作品，可以還原、永久刪除或全部清空。
/// </summary>
public partial class TrashViewModel(WorksApiClient api, IDialogService dialog) : ObservableObject
{
    public ObservableCollection<TrashItem> Items { get; } = [];

    [ObservableProperty] private string _statusMessage = "";

    /// <summary>讀取最近刪除的清單。視窗開啟時執行。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var trash = await api.GetTrashAsync();
            Items.Clear();
            // API 給的封面是相對網址，Image 要絕對網址才載得到
            foreach (var item in trash.Items)
                Items.Add(item with { CoverUrl = item.CoverUrl is { } url ? api.ToAbsoluteUrl(url) : null });

            StatusMessage = Items.Count == 0
                ? "最近刪除是空的"
                : $"共 {Items.Count} 部。刪除超過 {trash.KeepDays} 天的作品，會在 API 啟動時自動永久刪除。";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "讀取失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「還原」：作品回到主清單。</summary>
    [RelayCommand]
    private async Task RestoreAsync(TrashItem item)
    {
        try
        {
            await api.RestoreAsync(item.Id);
            Items.Remove(item);
            StatusMessage = $"已還原《{item.Title}》";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "還原失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「永久刪除」：確認後刪除，無法復原。</summary>
    [RelayCommand]
    private async Task PurgeAsync(TrashItem item)
    {
        if (!dialog.Confirm("永久刪除後無法復原，確定嗎？"))
            return;

        try
        {
            await api.PurgeAsync(item.Id);
            Items.Remove(item);
            StatusMessage = $"已永久刪除《{item.Title}》";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "刪除失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「全部清空」：確認後永久刪除所有項目。</summary>
    [RelayCommand]
    private async Task EmptyAsync()
    {
        if (Items.Count == 0 || !dialog.Confirm($"確定永久刪除這 {Items.Count} 部？此動作無法復原。"))
            return;

        try
        {
            var removed = await api.EmptyTrashAsync();
            Items.Clear();
            StatusMessage = $"已永久刪除 {removed} 部";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "清空失敗，請確認 Library.Api 有在執行";
        }
    }
}
