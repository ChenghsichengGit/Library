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
    private readonly IDialogService _dialog;

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

    // 表格目前選取的作品
    [ObservableProperty]
    private WorkItem? _selectedWork;

    // 正在編輯的作品 Id；null 代表這次是新增
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private int? _editingId;

    // 表單的各個欄位
    [ObservableProperty] private string _titleZh = "";
    [ObservableProperty] private string _titleJa = "";
    [ObservableProperty] private string _titleEn = "";
    [ObservableProperty] private string _remark = "";
    [ObservableProperty] private DateTime? _releaseDate;
    [ObservableProperty] private int _score;
    [ObservableProperty] private bool _favorite;
    [ObservableProperty] private bool _purchased;

    [ObservableProperty]
    private string _errorMessage = "";

    // 評分下拉選單的選項
    public int[] ScoreOptions { get; } = [0, 1, 2, 3, 4, 5, 6];

    // SelectedWork 改變時自動被呼叫：把選到的作品帶進表單
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
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "儲存失敗，請確認 Library.Api 有在執行";
        }
    }

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

    // 有正在編輯的作品（不是新增模式）才能刪除
    private bool CanDelete() => EditingId is not null;
}