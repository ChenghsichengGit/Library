using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 主視窗的 ViewModel：畫面上所有的資料和按鈕動作都在這裡。它不認識畫面，只改自己的屬性，畫面靠繫結自動更新。
/// </summary>
/// <remarks>
/// 兩個常用標記（CommunityToolkit.Mvvm 在編譯時自動產生程式碼）：
/// [ObservableProperty] 寫在欄位 _xxx 上 → 自動產生屬性 Xxx，設值時通知畫面。
///   除了宣告那一行，其他地方一律用大寫的 Xxx：直接改 _xxx 不會通知畫面，按鈕也不會重新判斷能不能按。
/// [RelayCommand] 寫在方法 XxxAsync() 或 Xxx() 上 → 自動產生 XxxCommand，按鈕用 {Binding XxxCommand} 繫結。
/// 類別要加 partial：自動產生的程式碼在另一個檔案，兩個合起來才是完整的類別。
/// </remarks>
public partial class MainViewModel : ObservableObject
{
    private readonly WorksApiClient _api;
    private readonly IDialogService _dialog;

    // 下拉選單的選項。寫成 static，下面的欄位初始值才能直接引用（欄位初始值不能用非 static 的成員）
    private static readonly SortOption[] AllSortOptions =
    [
        new("加入時間", "createdAt"),
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

// 篩選與排序條件（和上方工具列繫結）
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _onlyFavorite;
    [ObservableProperty] private ScoreFilter _selectedScoreFilter = AllScoreFilters[0];
    [ObservableProperty] private SortOption _selectedSort = AllSortOptions[0];
    [ObservableProperty] private bool _sortDescending = true;

    // 需要的東西從建構子傳進來（在 MainWindow.xaml.cs 建立），測試時可以換成假的
    public MainViewModel(WorksApiClient api, IDialogService dialog)
    {
        _api = api;
        _dialog = dialog;
    }

    // ObservableCollection：項目增減時會自動通知畫面，表格跟著更新
    public ObservableCollection<WorkItem> Works { get; } = [];

    /// <summary>類型下拉選單的選項，依顯示順序排列。</summary>
    public ObservableCollection<WorkTypeItem> WorkTypes { get; } = [];

    // 新增作品時預設第一個類型；清單是空的時給 0，存檔時由 API 回 400，而不是讓程式當掉
    private int DefaultWorkTypeId => WorkTypes.FirstOrDefault()?.Id ?? 0;

    // [ObservableProperty] 會自動產生 StatusMessage 屬性，值改變時通知畫面
    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>
    /// 依目前的搜尋、篩選、排序條件，從 API 讀取作品清單放進 Works。
    /// 由「重新載入」「搜尋」按鈕、搜尋框按 Enter、篩選條件改變時、視窗開啟時呼叫。
    /// </summary>
    // [RelayCommand] 會自動產生 LoadCommand，讓按鈕可以繫結（名稱規則：去掉 Async，加上 Command）
    [RelayCommand]
    private async Task LoadAsync()
    {
        // 啟動時 API 還沒開的話類型是空的，之後按「重新載入」時補讀
        if (WorkTypes.Count == 0)
            await LoadWorkTypesAsync();

        StatusMessage = "載入中…";
        try
        {
            // 用畫面上的條件組出查詢（Q: 這種寫法叫具名引數，參數多時不容易搞錯順序）
            var query = new WorkListQuery
            (
                Q: SearchText,
                // 沒勾 = 不篩選（null），不是「只要非最愛」（false）；送 false 的話最愛的作品全都會被篩掉
                Favorite: OnlyFavorite ? true : null,
                MinScore: SelectedScoreFilter.Min,
                MaxScore: SelectedScoreFilter.Max,
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
            // API 沒開是最常見的情況，顯示原因而不是讓程式當掉
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>
    /// 從 API 讀取類型清單。類型很少變動，只在清單還是空的時候由 LoadAsync 呼叫，不隨篩選重讀。
    /// </summary>
    [RelayCommand]
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

            // 選項讀進來之後才有預設值可以給；正在編輯作品時保留它原本的類型
            if (EditingId == null)
                WorkTypeId = DefaultWorkTypeId;
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    // 表格目前選取的作品（DataGrid 的 SelectedItem 繫結到這裡）
    [ObservableProperty]
    private WorkItem? _selectedWork;

    // 正在編輯的作品 Id；null 代表這次是新增
    // NotifyCanExecuteChangedFor：EditingId 一改變，就叫刪除按鈕重新判斷能不能按（見 CanDelete）
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private int? _editingId;

    // 表單的各個欄位（和右側的輸入框雙向繫結）
    [ObservableProperty] private string _titleZh = "";
    [ObservableProperty] private string _titleJa = "";
    [ObservableProperty] private string _titleEn = "";
    [ObservableProperty] private string _remark = "";
    // DatePicker 只認識 DateTime，所以表單用 DateTime?，送出時再轉成 DateOnly
    [ObservableProperty] private DateTime? _releaseDate;
    [ObservableProperty] private int _score;
    [ObservableProperty] private bool _favorite;
    [ObservableProperty] private bool _purchased;
    // 類型下拉選單以 SelectedValue 繫結到這裡（比對選項的 Id）
    [ObservableProperty] private int _workTypeId;
    // TextBox 只能繫結一個字串，所以作者、社團在表單裡是「一行一個名字」的文字，送出時才用 SplitLines 切成清單
    [ObservableProperty] private string _authorsInput = "";
    [ObservableProperty] private string _circlesInput = "";

    // 商店網址：只用來抓取資料填表單，不會存進作品
    [ObservableProperty] private string _storeUrl = "";

    // 紅字的錯誤訊息（驗證失敗、連不上 API）
    [ObservableProperty]
    private string _errorMessage = "";

    // 評分下拉選單的選項
    public int[] ScoreOptions { get; } = [0, 1, 2, 3, 4, 5, 6];

    /// <summary>
    /// SelectedWork 改變時自動被呼叫（點了表格某一列）：把選到的作品帶進表單。
    /// 名稱固定是 On + 屬性名稱 + Changed，[ObservableProperty] 會在設值時呼叫它。
    /// </summary>
    partial void OnSelectedWorkChanged(WorkItem? value)
    {
        // 重新載入清單時，選取會暫時變成 null；這時保留表單內容，不要清掉
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
        ErrorMessage = "";
    }

    /// <summary>「新增」按鈕（NewCommand）：清空表單，進入新增模式（EditingId = null）。</summary>
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
        StoreUrl = "";
        ErrorMessage = "";
    }

    /// <summary>
    /// 「儲存」按鈕（SaveCommand）：EditingId 有值就修改（PUT），沒有就新增（POST）。
    /// 輸入框的內容已經透過雙向繫結同步到 TitleZh 等屬性了，這裡直接拿來用。
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var request = new SaveWorkRequest(
            TitleZh, TitleJa, TitleEn, Remark,
            ReleaseDate is { } date ? DateOnly.FromDateTime(date) : null,
            Score, Favorite, Purchased, SplitLines(AuthorsInput), SplitLines(CirclesInput),
            WorkTypeId
        );

        try
        {
            int savedId;
            // EditingId is { } id：「EditingId 有值的話，把值（int）放進 id」
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
            await LoadAsync();

            // 重新載入後選回剛儲存的那筆，表單會更新成伺服器整理過的內容（例如去掉前後空白）
            SelectedWork = Works.FirstOrDefault(w => w.Id == savedId);
        }
        catch (ApiValidationException ex)
        {
            // API 回 400：顯示後端的驗證訊息（例如「至少輸入一個名稱」）
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "儲存失敗，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>「刪除」按鈕（DeleteCommand）：確認後刪除正在編輯的作品。只有 CanDelete() 為 true 時按得下去。</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        // EditingId 是 int?，API 要的是 int，所以先把值取出來
        if (EditingId is not { } id)
            return;

        if (!_dialog.Confirm("確認刪除?"))
            return;

        try
        {
            await _api.DeleteAsync(id);
            New();
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
    /// 「抓取」按鈕（FetchCommand）：依商店網址向 API 查詢，把抓到的資料填進表單。不會存檔，使用者確認後再按儲存。
    /// </summary>
    /// <remarks>
    /// 有值才覆蓋：商店沒有的欄位（例如沒有日文名稱）保留使用者已經填的內容；評分、最愛、備註完全不碰。
    /// 不改 EditingId：新增模式下抓取 → 儲存時新增；編輯中抓取 → 儲存時更新那部作品。
    /// 執行期間 FetchCommand 的 CanExecute 自動是 false，按鈕會變灰，不會被連按。
    /// </remarks>
    [RelayCommand]
    private async Task FetchAsync()
    {
        // 空白就不送出：API 的 url 參數是必填，送出去會收到一大段英文的驗證錯誤 JSON
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
            // 沒抓到日期（null）就用 ?? 保留原本的
            ReleaseDate = info.ReleaseDate?.ToDateTime(TimeOnly.MinValue) ?? ReleaseDate;

            ErrorMessage = "";
        }
        catch (ApiValidationException ex)
        {
            // API 回 400／404／502：顯示 API 給的訊息（不支援這個網站、找不到這個作品、無法連線到商店）
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "查詢失敗，請確認 Library.Api 有在執行";
        }
    }

    // 有正在編輯的作品（不是新增模式）才能刪除；回傳 false 時刪除按鈕會自動變灰
    private bool CanDelete() => EditingId is not null;

    // 篩選或排序的選項一改就重新查詢（屬性改變時自動呼叫的掛鉤）
    // SearchText 不在這裡：每打一個字都查太多次，所以要按 Enter 或「搜尋」才查
    partial void OnOnlyFavoriteChanged(bool value) => LoadCommand.Execute(null);
    partial void OnSelectedScoreFilterChanged(ScoreFilter value) => LoadCommand.Execute(null);
    partial void OnSelectedSortChanged(SortOption value) => LoadCommand.Execute(null);
    partial void OnSortDescendingChanged(bool value) => LoadCommand.Execute(null);

    /// <summary>把表單裡「一行一個名字」的文字切成清單，去掉前後空白和空行。</summary>
    /// <remarks>Windows 的 TextBox 換行是 \r\n：只用 \n 切開時每段結尾會留下 \r，靠 TrimEntries 一起去掉。</remarks>
    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    // 抓到的值是空的就保留表單原本的內容，有值才覆蓋
    private static string KeepIfEmpty(string fetched, string current) =>
        fetched == "" ? current : fetched;
}