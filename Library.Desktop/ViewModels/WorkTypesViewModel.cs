using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 類型管理視窗的 ViewModel：類型清單，以及新增、修改、刪除。
/// </summary>
public partial class WorkTypesViewModel : ObservableObject
{
    private readonly WorksApiClient _api;
    private readonly IDialogService _dialog;

    public WorkTypesViewModel(WorksApiClient api, IDialogService dialog)
    {
        _api = api;
        _dialog = dialog;
    }

    public ObservableCollection<WorkTypeItem> WorkTypes { get; } = [];

    [ObservableProperty] private WorkTypeItem? _selectedType;
    [ObservableProperty] private string _name = "";
    // 用字串接：清空輸入框時 int? 會轉換失敗而保留舊值
    [ObservableProperty] private string _sortOrderText = "";
    [ObservableProperty] private string _errorMessage = "";

    // 空白或不是數字就是 null，交給 API 決定順序
    private int? SortOrder => int.TryParse(SortOrderText, out var n) ? n : null;

    /// <summary>讀取類型清單。視窗開啟時與每次新增、修改、刪除後執行。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var types = await _api.GetWorkTypesAsync();
            WorkTypes.Clear();
            foreach (var type in types)
            {
                WorkTypes.Add(type);
            }

            ErrorMessage = "";
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "請確認 Library.Api 有在執行";
        }
    }

    /// <summary>點選清單某一列：把名稱和順序帶入表單。</summary>
    partial void OnSelectedTypeChanged(WorkTypeItem? value)
    {
        if (value == null) return;
        Name = value.Name;
        SortOrderText = value.SortOrder.ToString();
        ErrorMessage = "";
    }

    /// <summary>用表單內容新增類型；成功後清空表單，避免重複送出。</summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        try
        {
            await _api.CreateWorkTypeAsync(Name, SortOrder);
            ClearForm();
            await LoadAsync();
            ErrorMessage = "";
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "請確認 Library.Api 有在執行";
        }
    }

    /// <summary>修改選中的類型；表單保留，方便繼續調整。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedType == null) return;
        try
        {
            await _api.UpdateWorkTypeAsync(SelectedType.Id, Name, SortOrder);
            await LoadAsync();
            ErrorMessage = "";
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "請確認 Library.Api 有在執行";
        }
    }

    /// <summary>確認後刪除選中的類型。刪除「其他」時顯示 API 回傳的訊息。</summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedType == null) return;

        if (!_dialog.Confirm("使用這個類型的作品會改成『其他』，確定要刪除嗎？"))
            return;

        try
        {
            await _api.DeleteWorkTypeAsync(SelectedType.Id);
            ClearForm();
            await LoadAsync();
            ErrorMessage = "";
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "請確認 Library.Api 有在執行";
        }
    }

    private void ClearForm()
    {
        SelectedType = null;
        Name = "";
        SortOrderText = "";
    }
}
