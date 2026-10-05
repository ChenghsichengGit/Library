using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WorksApiClient _api;

    public MainViewModel(WorksApiClient api)
    {
        _api = api;
    }

    // ObservableCollection：項目增減時會自動通知畫面，表格跟著更新
    public ObservableCollection<WorkItem> Works { get; } = [];

    // [ObservableProperty] 會自動產生 StatusMessage 屬性，值改變時通知畫面
    [ObservableProperty]
    private string _statusMessage = "";

    // [RelayCommand] 會自動產生 LoadCommand，讓按鈕可以繫結
    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = "載入中…";
        try
        {
            var works = await _api.GetWorksAsync();
            Works.Clear();
            foreach (var work in works)
                Works.Add(work);
            StatusMessage = $"共 {works.Count} 部作品";
        }
        catch (HttpRequestException)
        {
            // API 沒開是最常見的情況，顯示原因而不是讓程式當掉
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }
}