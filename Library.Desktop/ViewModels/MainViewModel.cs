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

    // 需要的東西從建構子傳進來（在 MainWindow.xaml.cs 建立），測試時可以換成假的
    public MainViewModel(WorksApiClient api, IDialogService dialog)
    {
        _api = api;
        _dialog = dialog;
    }

    // ObservableCollection：項目增減時會自動通知畫面，表格跟著更新
    public ObservableCollection<WorkItem> Works { get; } = [];

    // [ObservableProperty] 會自動產生 StatusMessage 屬性，值改變時通知畫面
    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>
    /// 從 API 讀取作品清單，放進 Works。由「重新載入」按鈕（LoadCommand）和視窗開啟時呼叫。
    /// </summary>
    // [RelayCommand] 會自動產生 LoadCommand，讓按鈕可以繫結（名稱規則：去掉 Async，加上 Command）
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
            Score, Favorite, Purchased);

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

    // 有正在編輯的作品（不是新增模式）才能刪除；回傳 false 時刪除按鈕會自動變灰
    private bool CanDelete() => EditingId is not null;
}
