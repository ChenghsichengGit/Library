using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;
using Library.Desktop.ViewModels.Parts;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 主視窗的 ViewModel：清單、篩選、表單與按鈕動作。不認識畫面，只改自己的屬性，畫面靠繫結更新。
/// </summary>
/// <remarks>
/// 依功能拆成幾個 partial 檔：這裡是表單與新增／儲存／刪除；Filters 工具列、Loading 讀取清單與選項、
/// Lists 網址／DLC／系列建議、Cover 封面、Fetch 從商店抓取。
/// </remarks>
public partial class MainViewModel : ObservableObject
{
    private readonly WorksApiClient _api;
    private readonly IDialogService _dialog;

    public MainViewModel(WorksApiClient api, IDialogService dialog)
    {
        _api = api;
        _dialog = dialog;

        // 網址增減時也檢查重複；直接在網址欄打字不會觸發，儲存時會再檢查
        Urls.CollectionChanged += (_, _) => ScheduleDuplicateCheck();
        Dlcs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(DlcHeader));
    }

    // ── 清單與狀態 ──

    public ObservableCollection<WorkItem> Works { get; } = [];

    [ObservableProperty]
    private WorkItem? _selectedWork;

    // 正在編輯的作品 Id；null 代表新增模式。改變時讓刪除按鈕重新判斷能不能按
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private int? _editingId;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _errorMessage = "";

    // ── 表單欄位（右側輸入框） ──

    [ObservableProperty] private string _titleZh = "";
    [ObservableProperty] private string _titleJa = "";
    [ObservableProperty] private string _titleEn = "";

    [ObservableProperty] private string _description = "";

    // DatePicker 只認識 DateTime，送出時再轉成 DateOnly
    [ObservableProperty] private DateTime? _releaseDate;
    [ObservableProperty] private int _score;
    [ObservableProperty] private bool _favorite;

    public int[] ScoreOptions { get; } = [0, 1, 2, 3, 4, 5, 6];

    /// <summary>類型下拉選單的選項，依顯示順序排列。</summary>
    public ObservableCollection<WorkTypeItem> WorkTypes { get; } = [];

    // 類型下拉選單以 SelectedValue 繫結到這裡（比對選項的 Id）
    [ObservableProperty] private int _workTypeId;

    // 新增作品時預設第一個類型；清單是空的時給 0，存檔時由 API 回 400，而不是讓程式當掉
    private int DefaultWorkTypeId => WorkTypes.FirstOrDefault()?.Id ?? 0;

    // 作者、社團、標籤：已加入的名字是標籤塊，輸入框有自動完成（打逗號或按 Enter 加入）
    public NameListEditor Authors { get; } = new();
    public NameListEditor Circles { get; } = new();
    public NameListEditor Tags { get; } = new();

    [ObservableProperty] private string _seriesInput = "";

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
        Description = value.Description;
        ReleaseDate = value.ReleaseDate?.ToDateTime(TimeOnly.MinValue);
        Score = value.Score;
        Favorite = value.Favorite;
        WorkTypeId = value.WorkTypeId;
        Authors.SetNames(value.Authors);
        Circles.SetNames(value.Circles);
        Tags.SetNames(value.Tags);
        SeriesInput = value.Series ?? "";
        CoverKey = value.CoverKey;
        CoverPreviewUrl = value.CoverUrl is { } coverUrl ? _api.ToAbsoluteUrl(coverUrl) : null;
        ErrorMessage = "";
        CoverStatus = "";
        StoreUrl = "";

        Urls.Clear();
        foreach (var url in value.Urls)
        {
            Urls.Add(UrlRow.From(url));
        }

        Dlcs.Clear();
        foreach (var dlc in value.Dlcs)
        {
            Dlcs.Add(DlcRow.From(dlc));
        }
    }

    // ── 新增、儲存、刪除 ──

    /// <summary>「新增」：清空表單，進入新增模式。</summary>
    [RelayCommand]
    private void New()
    {
        SelectedWork = null;
        EditingId = null;
        TitleZh = "";
        TitleJa = "";
        TitleEn = "";
        Description = "";
        ReleaseDate = null;
        Score = 0;
        Favorite = false;
        WorkTypeId = DefaultWorkTypeId;
        Authors.SetNames([]);
        Circles.SetNames([]);
        Tags.SetNames([]);
        StoreUrl = "";
        SeriesInput = "";
        ErrorMessage = "";
        Urls.Clear();
        Dlcs.Clear();
        CoverKey = null;
        CoverPreviewUrl = null;
        CoverStatus = "";
    }

    /// <summary>點了視窗的空白處：編輯中才回到新增模式；已經在新增模式就不動，打到一半的新作品才不會被清掉。</summary>
    [RelayCommand]
    private void Deselect()
    {
        if (EditingId is null)
            return;

        New();
    }

    /// <summary>「儲存」：編輯中就修改（PUT），新增模式就新增（POST）。驗證失敗時顯示 API 的訊息。</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var request = new SaveWorkRequest(
            TitleZh, TitleJa, TitleEn, Description,
            ReleaseDate is { } date ? DateOnly.FromDateTime(date) : null,
            Score, Favorite, Authors.GetNames(), Circles.GetNames(),
            WorkTypeId, Tags.GetNames(), SeriesInput, Urls.Select(r => r.ToItem()).ToList(),
            Dlcs.Select(d => d.ToItem()).ToList(), CoverKey);

        try
        {
            if (!await ConfirmIfDuplicateAsync())
                return;

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
            await LoadCreatorsAsync();

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

        if (!_dialog.Confirm("把這筆作品移到最近刪除？之後可以還原。"))
            return;

        try
        {
            await _api.DeleteAsync(id);
            New();
            await LoadTagsAsync();
            await LoadSeriesAsync();
            await LoadAsync();
            await LoadCreatorsAsync();
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

    private bool CanSave() => !IsCoverBusy;
    private bool CanDelete() => EditingId is not null;

    // ── 子視窗 ──

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
    /// 「管理作者／系列」與資訊列的「編輯」：開啟管理視窗，關閉後重讀（名稱、網址、封面可能改了）。
    /// </summary>
    [RelayCommand]
    private async Task ManageCreatorsAsync(int? selectCreatorId)
    {
        _dialog.ShowCreatorSeriesManager(selectCreatorId);

        await LoadCreatorsAsync();
        await LoadSeriesAsync();
        await LoadAsync();

        // 編輯中的作品重新帶入表單：作者、系列的名字可能改了
        if (EditingId is { } id)
            SelectedWork = Works.FirstOrDefault(w => w.Id == id);
    }
}
