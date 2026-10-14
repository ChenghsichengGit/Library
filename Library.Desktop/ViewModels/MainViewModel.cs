using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 主視窗的 ViewModel：清單、篩選、表單與按鈕動作。不認識畫面，只改自己的屬性，畫面靠繫結更新。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly WorksApiClient _api;
    private readonly IDialogService _dialog;

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

    /// <summary>標籤篩選列的選項，勾選狀態存在各選項裡（ListBox.SelectedItems 無法繫結）。</summary>
    public ObservableCollection<TagFilterOption> TagFilters { get; } = [];

    // 篩選與排序條件（上方工具列）
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _onlyFavorite;

    [ObservableProperty] private ScoreFilter _selectedScoreFilter = AllScoreFilters[0];

    // 0 = 全部類型，送 API 時換成 null
    [ObservableProperty] private int _filterWorkTypeId;
    [ObservableProperty] private bool _tagMatchAny;
    [ObservableProperty] private SortOption _selectedSort = AllSortOptions[0];
    [ObservableProperty] private bool _sortDescending = true;

    public MainViewModel(WorksApiClient api, IDialogService dialog)
    {
        _api = api;
        _dialog = dialog;
    }

    public ObservableCollection<WorkItem> Works { get; } = [];

    /// <summary>類型下拉選單的選項，依顯示順序排列。</summary>
    public ObservableCollection<WorkTypeItem> WorkTypes { get; } = [];

    /// <summary>工具列的類型篩選選項：第一項「全部類型」（Id 0），其餘同 WorkTypes。</summary>
    public ObservableCollection<WorkTypeItem> WorkTypeFilters { get; } = [];

    /// <summary>系列輸入框的選項。存檔可能建立新系列，所以儲存、刪除後重讀。</summary>
    public ObservableCollection<SeriesItem> SeriesOptions { get; } = [];

    // 新增作品時預設第一個類型；清單是空的時給 0，存檔時由 API 回 400，而不是讓程式當掉
    private int DefaultWorkTypeId => WorkTypes.FirstOrDefault()?.Id ?? 0;

    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>
    /// 依目前的搜尋、篩選、排序條件讀取作品清單。視窗開啟、按重新載入／搜尋、篩選條件改變時執行。
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        // 啟動時 API 還沒開的話類型是空的，之後按「重新載入」時補讀
        if (WorkTypes.Count == 0)
            await LoadWorkTypesAsync();

        if (TagFilters.Count == 0)
            await LoadTagsAsync();

        if (SeriesOptions.Count == 0)
            await LoadSeriesAsync();

        StatusMessage = "載入中…";
        try
        {
            var query = new WorkListQuery
            (
                Q: SearchText,
                // 沒勾是「不篩選」（null），送 false 會變成「只要非最愛」
                Favorite: OnlyFavorite ? true : null,
                MinScore: SelectedScoreFilter.Min,
                MaxScore: SelectedScoreFilter.Max,
                WorkTypeId: FilterWorkTypeId == 0 ? null : FilterWorkTypeId,
                Tags: TagFilters
                    .Where(t => t.IsSelected)
                    .Select(t => t.Name)
                    .ToList(),
                TagAny: TagMatchAny,
                Sort: SelectedSort.Value,
                Desc: SortDescending
            );
            var works = await _api.GetWorksAsync(query);
            Works.Clear();
            foreach (var work in works)
                Works.Add(work);
            StatusMessage = $"共 {works.Count} 部作品";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>
    /// 從 API 讀取類型清單。類型很少變動，只在清單還是空的時候由 LoadAsync 呼叫，不隨篩選重讀。
    /// </summary>
    private async Task LoadWorkTypesAsync()
    {
        try
        {
            var workType = await _api.GetWorkTypesAsync();
            WorkTypes.Clear();
            foreach (var type in workType)
            {
                WorkTypes.Add(type);
            }

            WorkTypeFilters.Clear();
            WorkTypeFilters.Add(new WorkTypeItem(0, "全部類型", 0));
            foreach (var type in workType)
            {
                WorkTypeFilters.Add(type);
            }

            // 篩選中的類型被刪掉就回到全部；否則通知繫結重讀，Clear() 後下拉選單才會選回原本的項目
            if (FilterWorkTypeId != 0 && WorkTypes.All(t => t.Id != FilterWorkTypeId))
                FilterWorkTypeId = 0;
            else
                OnPropertyChanged(nameof(FilterWorkTypeId));

            // 選項讀進來之後才有預設值可以給；正在編輯作品時保留它原本的類型
            if (EditingId == null)
                WorkTypeId = DefaultWorkTypeId;
            // 值沒變時 setter 不會通知，Clear() 後下拉選單要靠這行才會選回來
            OnPropertyChanged(nameof(WorkTypeId));
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>從 API 讀取系列清單。</summary>
    private async Task LoadSeriesAsync()
    {
        try
        {
            var series = await _api.GetSeriesAsync();
            SeriesOptions.Clear();
            foreach (var item in series)
            {
                SeriesOptions.Add(item);
            }
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    [ObservableProperty]
    private WorkItem? _selectedWork;

    // 正在編輯的作品 Id；null 代表新增模式。改變時讓刪除按鈕重新判斷能不能按
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private int? _editingId;

    // 表單欄位（右側輸入框）
    [ObservableProperty] private string _titleZh = "";
    [ObservableProperty] private string _titleJa = "";
    [ObservableProperty] private string _titleEn = "";

    [ObservableProperty] private string _remark = "";

    // DatePicker 只認識 DateTime，送出時再轉成 DateOnly
    [ObservableProperty] private DateTime? _releaseDate;
    [ObservableProperty] private int _score;
    [ObservableProperty] private bool _favorite;

    [ObservableProperty] private bool _purchased;

    // 類型下拉選單以 SelectedValue 繫結到這裡（比對選項的 Id）
    [ObservableProperty] private int _workTypeId;

    // 作者、社團在表單裡是一行一個名字，送出時才用 SplitLines 切成清單
    [ObservableProperty] private string _authorsInput = "";
    [ObservableProperty] private string _circlesInput = "";

    [ObservableProperty] private string _tagsInput = "";
    [ObservableProperty] private string _seriesInput = "";

    // 只用來抓取資料填表單，不會存進作品
    [ObservableProperty] private string _storeUrl = "";

    [ObservableProperty]
    private string _errorMessage = "";

    public int[] ScoreOptions { get; } = [0, 1, 2, 3, 4, 5, 6];

    /// <summary>點了表格某一列：把選到的作品帶進表單。</summary>
    partial void OnSelectedWorkChanged(WorkItem? value)
    {
        // 重新載入清單時選取會暫時變成 null，這時保留表單內容
        if (value is null)
            return;

        EditingId = value.Id;
        TitleZh = value.TitleZh;
        TitleJa = value.TitleJa;
        TitleEn = value.TitleEn;
        Remark = value.Remark;
        ReleaseDate = value.ReleaseDate?.ToDateTime(TimeOnly.MinValue);
        Score = value.Score;
        Favorite = value.Favorite;
        Purchased = value.Purchased;
        WorkTypeId = value.WorkTypeId;
        AuthorsInput = string.Join("\n", value.Authors);
        CirclesInput = string.Join("\n", value.Circles);
        TagsInput = string.Join("\n", value.Tags);
        SeriesInput = value.Series ?? "";
        ErrorMessage = "";
    }

    /// <summary>「新增」：清空表單，進入新增模式。</summary>
    [RelayCommand]
    private void New()
    {
        SelectedWork = null;
        EditingId = null;
        TitleZh = "";
        TitleJa = "";
        TitleEn = "";
        Remark = "";
        ReleaseDate = null;
        Score = 0;
        Favorite = false;
        Purchased = false;
        WorkTypeId = DefaultWorkTypeId;
        AuthorsInput = "";
        CirclesInput = "";
        TagsInput = "";
        StoreUrl = "";
        SeriesInput = "";
        ErrorMessage = "";
    }

    /// <summary>「儲存」：編輯中就修改（PUT），新增模式就新增（POST）。驗證失敗時顯示 API 的訊息。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var request = new SaveWorkRequest(
            TitleZh, TitleJa, TitleEn, Remark,
            ReleaseDate is { } date ? DateOnly.FromDateTime(date) : null,
            Score, Favorite, Purchased, SplitLines(AuthorsInput), SplitLines(CirclesInput),
            WorkTypeId, SplitLines(TagsInput), SeriesInput
        );

        try
        {
            int savedId;
            if (EditingId is { } id)
            {
                await _api.UpdateAsync(id, request);
                savedId = id;
            }
            else
            {
                savedId = (await _api.CreateAsync(request)).Id;
            }

            ErrorMessage = "";
            // 先更新標籤：LoadAsync 依勾選組查詢條件，已消失的標籤不能再送
            await LoadTagsAsync();
            await LoadSeriesAsync();
            await LoadAsync();

            // 選回剛儲存的那筆，表單更新成伺服器整理過的內容（例如去掉前後空白）
            SelectedWork = Works.FirstOrDefault(w => w.Id == savedId);
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "儲存失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「刪除」：確認後刪除正在編輯的作品。</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (EditingId is not { } id)
            return;

        if (!_dialog.Confirm("確認刪除?"))
            return;

        try
        {
            await _api.DeleteAsync(id);
            New();
            await LoadTagsAsync();
            await LoadSeriesAsync();
            await LoadAsync();
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "刪除失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>
    /// 「抓取」：依商店網址查詢，把資料填進表單，不存檔。
    /// </summary>
    /// <remarks>
    /// 有值才覆蓋：商店沒有的欄位保留使用者已填的內容；評分、最愛、備註不碰。
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
            AuthorsInput = KeepIfEmpty(string.Join("\n", info.Authors), AuthorsInput);
            CirclesInput = KeepIfEmpty(string.Join("\n", info.Circles), CirclesInput);
            ReleaseDate = info.ReleaseDate?.ToDateTime(TimeOnly.MinValue) ?? ReleaseDate;

            ErrorMessage = "";
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

    /// <summary>
    /// 「管理類型」：開啟類型管理視窗，關閉後重新讀取類型與作品（類型可能被改名或刪除）。
    /// </summary>
    [RelayCommand]
    private async Task ManageWorkTypesAsync()
    {
        _dialog.ShowWorkTypeManager();

        // LoadAsync 只在類型清單是空的時候才讀，所以先自己讀
        await LoadWorkTypesAsync();
        await LoadAsync();

        // 編輯中的作品重新帶入表單：它的類型若被刪除，已在資料庫改成「其他」
        if (EditingId is { } id)
            SelectedWork = Works.FirstOrDefault(w => w.Id == id);
    }

    /// <summary>
    /// 讀取標籤篩選清單，依名稱保留原本的勾選。啟動時及作品儲存、刪除後執行（作品數會變）。
    /// </summary>
    private async Task LoadTagsAsync()
    {
        try
        {
            var selected = TagFilters
                .Where(t => t.IsSelected)
                .Select(t => t.Name)
                .ToHashSet();

            var tags = await _api.GetTagsAsync();

            TagFilters.Clear();
            foreach (var tag in tags)
            {
                TagFilters.Add(new TagFilterOption(tag, selected.Contains(tag.Name), () => LoadCommand.Execute(null)));
            }
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    private bool CanDelete() => EditingId is not null;

    // 篩選或排序一改就重新查詢；搜尋字不在這裡，每打一個字都查太多次
    partial void OnOnlyFavoriteChanged(bool value) => LoadCommand.Execute(null);
    partial void OnSelectedScoreFilterChanged(ScoreFilter value) => LoadCommand.Execute(null);
    partial void OnFilterWorkTypeIdChanged(int value) => LoadCommand.Execute(null);
    partial void OnSelectedSortChanged(SortOption value) => LoadCommand.Execute(null);
    partial void OnSortDescendingChanged(bool value) => LoadCommand.Execute(null);
    partial void OnTagMatchAnyChanged(bool value) => LoadCommand.Execute(null);

    // TextBox 的換行是 \r\n，用 \n 切開後靠 TrimEntries 去掉 \r
    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static string KeepIfEmpty(string fetched, string current) =>
        fetched == "" ? current : fetched;
}