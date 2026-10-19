using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.ViewModels.Parts;

namespace Library.Desktop.ViewModels;

/// <summary>工具列：搜尋、篩選、排序，以及篩選某位作者／社團時的資訊列。</summary>
public partial class MainViewModel
{
    // static：下面的欄位初始值才能引用
    private static readonly SortOption[] AllSortOptions =
    [
        new("加入時間", "createdAt"),
        new("修改時間", "updatedAt"),
        new("上架日", "releaseDate"),
        new("評分", "score"),
        new("作品名", "title"),
    ];

    private static readonly ScoreFilter[] AllScoreFilters =
    [
        new("全部評分", null, null),
        new("王冠", 6, null),
        new("五星以上", 5, null),
        new("三星以上", 3, null),
        new("未評價", null, 0),
    ];

    public SortOption[] SortOptions => AllSortOptions;
    public ScoreFilter[] ScoreFilters => AllScoreFilters;

    // ── 搜尋、篩選、排序 ──

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _onlyFavorite;

    [ObservableProperty] private ScoreFilter _selectedScoreFilter = AllScoreFilters[0];

    // 0 = 全部類型，送 API 時換成 null
    [ObservableProperty] private int _filterWorkTypeId;

    /// <summary>工具列的類型篩選選項：第一項「全部類型」（Id 0），其餘同 WorkTypes。</summary>
    public ObservableCollection<WorkTypeItem> WorkTypeFilters { get; } = [];

    // 只看還有 DLC 沒買的作品
    [ObservableProperty] private bool _dlcUnbought;

    [ObservableProperty] private SortOption _selectedSort = AllSortOptions[0];
    [ObservableProperty] private bool _sortDescending = true;

    // ── 標籤篩選 ──

    /// <summary>標籤篩選列的選項，勾選狀態存在各選項裡（ListBox.SelectedItems 無法繫結）。</summary>
    public ObservableCollection<TagFilterOption> TagFilters { get; } = [];

    [ObservableProperty] private bool _tagMatchAny;

    /// <summary>標籤篩選區的標題；收合時也看得出有沒有勾選標籤。</summary>
    public string TagFilterHeader =>
        TagFilters.Count(t => t.IsSelected) is var n and > 0 ? $"標籤篩選（已選 {n} 個）" : "標籤篩選";

    // ── 作者／社團、系列篩選 ──

    // 工具列的作者／社團篩選：作者和社團合併成一個清單，Id 0 是「全部作者／社團」
    public ObservableCollection<CreatorItem> CreatorFilters { get; } = [];

    [ObservableProperty] private int _filterCreatorId;

    // 系列篩選：選了作者時只列他參與的；和表單自動完成用的 SeriesOptions 分開
    public ObservableCollection<SeriesItem> SeriesFilters { get; } = [];

    [ObservableProperty] private int _filterSeriesId;

    /// <summary>篩選中的作者／社團，給資訊列顯示；沒有篩選時是 null。</summary>
    public CreatorItem? FilterCreator => CreatorFilters.FirstOrDefault(c => c.Id == FilterCreatorId && c.Id != 0);

    // 換作者：資訊列更新、系列選單改成他的系列，再重新查詢
    partial void OnFilterCreatorIdChanged(int value)
    {
        OnPropertyChanged(nameof(FilterCreator));
        ApplyCreatorFilterCommand.Execute(null);
    }

    [RelayCommand]
    private async Task ApplyCreatorFilterAsync()
    {
        await LoadSeriesFiltersAsync();
        LoadCommand.Execute(null);
    }

    /// <summary>資訊列的「×」：取消作者／社團篩選。</summary>
    [RelayCommand]
    private void ClearCreatorFilter() => FilterCreatorId = 0;

    /// <summary>資訊列的「網站」：開啟篩選中作者／社團的網址，只接受 http、https。</summary>
    [RelayCommand]
    private void OpenCreatorUrl()
    {
        if (FilterCreator == null
            || !Uri.TryCreate(FilterCreator.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        _dialog.OpenInBrowser(uri);
    }

    // ── 條件一改就重新查詢 ──

    // 搜尋字不在這裡，每打一個字都查太多次；作者在上面另外處理
    partial void OnOnlyFavoriteChanged(bool value) => LoadCommand.Execute(null);
    partial void OnSelectedScoreFilterChanged(ScoreFilter value) => LoadCommand.Execute(null);
    partial void OnFilterWorkTypeIdChanged(int value) => LoadCommand.Execute(null);
    partial void OnDlcUnboughtChanged(bool value) => LoadCommand.Execute(null);
    partial void OnSelectedSortChanged(SortOption value) => LoadCommand.Execute(null);
    partial void OnSortDescendingChanged(bool value) => LoadCommand.Execute(null);
    partial void OnTagMatchAnyChanged(bool value) => LoadCommand.Execute(null);
    partial void OnFilterSeriesIdChanged(int value) => LoadCommand.Execute(null);
}
