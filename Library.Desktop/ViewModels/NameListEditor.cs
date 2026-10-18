using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 作者、社團、標籤這類「多個名字」的輸入：已加入的名字（標籤塊）、輸入框的文字與自動完成建議。
/// </summary>
public partial class NameListEditor : ObservableObject
{
    private const int MaxSuggestions = 8;

    // 自動完成的來源（從 API 讀進來的全部名字）
    private List<NameOption> _options = [];

    /// <summary>已加入的名字，畫面上的一個個標籤塊。</summary>
    public ObservableCollection<string> Names { get; } = [];

    /// <summary>名字包含輸入文字、還沒加入的選項，最多 8 筆。</summary>
    public ObservableCollection<NameOption> Suggestions { get; } = [];

    [ObservableProperty] private string _inputText = "";

    /// <summary>自動完成的來源讀過了沒（啟動時 API 沒開，之後按重新載入要補讀）。</summary>
    public bool HasOptions => _options.Count > 0;

    /// <summary>換一批自動完成的來源（存檔後作品數會變，要重讀）。</summary>
    public void SetOptions(IEnumerable<NameOption> options) => _options = options.ToList();

    /// <summary>載入某部作品的名字，清空輸入框。</summary>
    public void SetNames(IEnumerable<string> names)
    {
        Names.Clear();
        foreach (var name in names)
            Names.Add(name);
        InputText = "";
    }

    /// <summary>
    /// 送出時用：已加入的名字，加上輸入框裡還沒按 Enter 的那一個（不然打完直接按儲存會不見）。
    /// </summary>
    public List<string> GetNames() =>
        string.IsNullOrWhiteSpace(InputText) ? Names.ToList() : [.. Names, InputText.Trim()];

    /// <summary>加入一個名字（按 Enter 或選了建議）：加完清空輸入框。</summary>
    [RelayCommand]
    private void Add(string? name)
    {
        // 沒有傳入參數時（直接按 Enter）用輸入框的文字
        AddName(name ?? InputText);
        InputText = "";
    }

    // 去前後空白，空的或已經有（不分大小寫）就不加
    private void AddName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed == "" || Names.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            return;

        Names.Add(trimmed);
    }

    [RelayCommand]
    private void Remove(string name) => Names.Remove(name);

    // 每打一個字就重新篩選建議
    partial void OnInputTextChanged(string value)
    {
        // 打了逗號（半形或全形）：最後一個逗號前面的每一段都加入，後面的留在輸入框；貼上 "A, B" 也一次加完
        var lastComma = value.LastIndexOfAny([',', '，']);
        if (lastComma >= 0)
        {
            foreach (var part in value[..lastComma].Split([',', '，']))
                AddName(part);

            // 設定後會再進來一次，那次沒有逗號，照常篩選建議
            InputText = value[(lastComma + 1)..];
            return;
        }

        Suggestions.Clear();

        var text = value.Trim();
        if (text == "")
            return;

        var matches = _options
            .Where(o => o.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                        && !Names.Contains(o.Name, StringComparer.OrdinalIgnoreCase))
            .Take(MaxSuggestions);

        foreach (var option in matches)
            Suggestions.Add(option);
    }

    /// <summary>輸入框是空的時按 Backspace：刪除最後一個名字。有刪除才回傳 true（呼叫端要攔下按鍵）。</summary>
    public bool RemoveLastIfInputEmpty()
    {
        if (InputText != "" || Names.Count == 0)
            return false;

        Names.RemoveAt(Names.Count - 1);
        return true;
    }
}
