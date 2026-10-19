using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 作者／社團、系列的管理視窗：清單，以及修改名稱、網址、備註、封面。
/// </summary>
public partial class CreatorSeriesViewModel : ObservableObject
{
    private readonly WorksApiClient _api;
    private readonly IDialogService _dialog;

    // 讀完清單後要選取的 Id：從資訊列開啟時是那個人，儲存後是剛存的那一筆
    private int? _pendingSelectId;

    public CreatorSeriesViewModel(WorksApiClient api, IDialogService dialog, int? selectCreatorId = null)
    {
        _api = api;
        _dialog = dialog;
        _pendingSelectId = selectCreatorId;
    }

    public string[] Kinds { get; } = ["作者／社團", "系列"];

    [ObservableProperty] private int _kindIndex;

    public bool IsSeries => KindIndex == 1;

    public ObservableCollection<ManageEntry> Entries { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private ManageEntry? _selectedEntry;

    // 表單
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _errorMessage = "";

    // 封面：key 存檔時送出，網址給預覽；CoverChoices 是可以挑的作品封面
    [ObservableProperty] private string? _coverKey;
    [ObservableProperty] private string? _coverPreviewUrl;
    [ObservableProperty] private string _coverStatus = "";

    public ObservableCollection<CoverChoice> CoverChoices { get; } = [];

    // 上傳中不能儲存，否則存進去的是舊的 CoverKey
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    /// <summary>讀取目前分頁的清單。視窗開啟、切換分頁、儲存後執行。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            List<ManageEntry> entries;
            if (IsSeries)
            {
                entries = (await _api.GetSeriesAsync()).Select(ManageEntry.From).ToList();
            }
            else
            {
                var authors = await _api.GetCreatorsAsync("author");
                var circles = await _api.GetCreatorsAsync("circle");
                // 同一個人可能同時是作者和社團
                entries = authors.Concat(circles)
                    .DistinctBy(a => a.Id)
                    .OrderBy(c => c.Name)
                    .Select(ManageEntry.From)
                    .ToList();
            }

            Entries.Clear();
            foreach (var entry in entries)
                Entries.Add(entry);

            SelectedEntry = Entries.FirstOrDefault(e => e.Id == _pendingSelectId);
            _pendingSelectId = null;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "請確認 Library.Api 有在執行";
        }
    }

    partial void OnKindIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSeries));
        LoadCommand.Execute(null);
    }

    /// <summary>點選清單某一列：帶入表單，並讀取可以挑選的封面。</summary>
    partial void OnSelectedEntryChanged(ManageEntry? value)
    {
        Name = value?.Name ?? "";
        Url = value?.Url ?? "";
        Note = value?.Note ?? "";
        CoverKey = value?.CoverKey;
        CoverPreviewUrl = value?.CoverUrl is { } coverUrl ? _api.ToAbsoluteUrl(coverUrl) : null;
        // 有網址卻沒有 key：顯示的是最新作品的封面
        CoverStatus = value is { CoverKey: null, CoverUrl: not null } ? "未設定：顯示最新作品的封面" : "";
        ErrorMessage = "";
        LoadCoverChoicesCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadCoverChoicesAsync()
    {
        CoverChoices.Clear();
        if (SelectedEntry is not { } entry)
            return;

        try
        {
            var choices = IsSeries
                ? await _api.GetSeriesCoverChoicesAsync(entry.Id)
                : await _api.GetCreatorCoverChoicesAsync(entry.Id);
            foreach (var choice in choices)
                CoverChoices.Add(choice with { CoverUrl = _api.ToAbsoluteUrl(choice.CoverUrl) });
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "請確認 Library.Api 有在執行";
        }
    }

    /// <summary>點某張作品的封面：換成那張（按儲存才會生效）。</summary>
    [RelayCommand]
    private void PickCover(CoverChoice choice)
    {
        CoverKey = choice.CoverKey;
        CoverPreviewUrl = choice.CoverUrl;
        CoverStatus = $"使用「{choice.Title}」的封面";
    }

    /// <summary>「清除」：不設定封面，儲存後改用最新作品的封面。</summary>
    [RelayCommand]
    private void ClearCover()
    {
        CoverKey = null;
        CoverPreviewUrl = null;
        CoverStatus = "未設定：儲存後改用最新作品的封面";
    }

    /// <summary>「上傳圖片」：選檔、縮圖後上傳。</summary>
    [RelayCommand]
    private async Task UploadCoverAsync()
    {
        var path = _dialog.PickImageFile();
        if (path is null)
            return;

        IsBusy = true;
        try
        {
            var original = await File.ReadAllBytesAsync(path);
            var content = await Task.Run(() => CoverImageShrinker.Shrink(original));
            var result = await _api.UploadCoverAsync(content, "cover");
            CoverKey = result.Key;
            CoverPreviewUrl = _api.ToAbsoluteUrl(result.Url);
            CoverStatus = "封面已上傳";
            ErrorMessage = "";
        }
        catch (ApiValidationException ex)
        {
            ErrorMessage = string.Join("\n", ex.Messages);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "上傳失敗，請確認 Library.Api 有在執行";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>儲存修改；成功後重讀清單並選回這一筆。</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (SelectedEntry is not { } entry)
            return;

        try
        {
            if (IsSeries)
                await _api.UpdateSeriesAsync(entry.Id, Name, Note, CoverKey);
            else
                await _api.UpdateCreatorAsync(entry.Id, Name, Url, Note, CoverKey);

            ErrorMessage = "";
            _pendingSelectId = entry.Id;
            await LoadAsync();
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

    private bool CanSave() => SelectedEntry != null && !IsBusy;
}
