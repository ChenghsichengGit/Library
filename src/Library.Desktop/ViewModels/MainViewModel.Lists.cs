using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.ViewModels.Parts;

namespace Library.Desktop.ViewModels;

/// <summary>表單裡可以增減的清單：商店網址、DLC，以及系列輸入框的建議。</summary>
public partial class MainViewModel
{
    /// <summary>網址的語言選項；"" 是未指定。</summary>
    private static readonly UrlLanguageOption[] AllUrlLanguageOptions =
    [
        new("未指定", ""),
        new("中文", "zh"),
        new("日文", "ja"),
        new("英文", "en")
    ];

    public UrlLanguageOption[] UrlLanguageOptions => AllUrlLanguageOptions;

    // ── 商店網址 ──

    // 商店網址，每列各自有購買狀態；作品的「已購買」由 API 推導，表單不能直接改
    public ObservableCollection<UrlRow> Urls { get; } = [];

    /// <summary>表單的網址清單多一列空白的；沒填就存檔時由 API 略過。</summary>
    [RelayCommand]
    private void AddUrl() => Urls.Add(new UrlRow());

    // 帶參數的命令：每一列的按鈕以 CommandParameter="{Binding}" 傳入自己
    [RelayCommand]
    private void RemoveUrl(UrlRow row) => Urls.Remove(row);

    // 清單順序就是存檔的 Position；Move 只移動那一列，畫面不會整個重建
    [RelayCommand]
    private void MoveUrlUp(UrlRow row)
    {
        var i = Urls.IndexOf(row);
        if (i > 0)
            Urls.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveUrlDown(UrlRow row)
    {
        var i = Urls.IndexOf(row);
        if (i < Urls.Count - 1)
            Urls.Move(i, i + 1);
    }

    // ── DLC ──

    // DLC／追加內容，不影響作品的「已購買」
    public ObservableCollection<DlcRow> Dlcs { get; } = [];

    /// <summary>表單的 DLC 清單多一列空白的；沒填名稱就存檔時由 API 略過。</summary>
    [RelayCommand]
    private void AddDlc() => Dlcs.Add(new DlcRow());

    [RelayCommand]
    private void RemoveDlc(DlcRow row) => Dlcs.Remove(row);

    // 清單順序就是存檔的 Position；Move 只移動那一列，畫面不會整個重建
    [RelayCommand]
    private void MoveDlcUp(DlcRow row)
    {
        var i = Dlcs.IndexOf(row);
        if (i > 0)
            Dlcs.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveDlcDown(DlcRow row)
    {
        var i = Dlcs.IndexOf(row);
        if (i < Dlcs.Count - 1)
            Dlcs.Move(i, i + 1);
    }

    // ── 系列建議 ──

    /// <summary>系列輸入框的選項。存檔可能建立新系列，所以儲存、刪除後重讀。</summary>
    public ObservableCollection<SeriesItem> SeriesOptions { get; } = [];

    /// <summary>系列輸入框的建議：名稱包含輸入文字的，目前作者的系列排前面，最多 8 筆。</summary>
    public ObservableCollection<SeriesItem> SeriesSuggestions { get; } = [];

    // 每打一個字就重新篩選系列建議
    partial void OnSeriesInputChanged(string value)
    {
        SeriesSuggestions.Clear();

        var text = value.Trim();
        if (text == "")
            return;

        var authors = Authors.GetNames();
        var matches = SeriesOptions
            .Where(s => s.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            // true 排前面：系列的作者裡有表單上的任何一位作者
            .OrderByDescending(s => s.Authors.Any(a => authors.Contains(a, StringComparer.OrdinalIgnoreCase)))
            .Take(8);

        foreach (var series in matches)
            SeriesSuggestions.Add(series);
    }
}
