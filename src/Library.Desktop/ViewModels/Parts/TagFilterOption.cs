using CommunityToolkit.Mvvm.ComponentModel;
using Library.Desktop.Models;

namespace Library.Desktop.ViewModels.Parts;

/// <summary>標籤篩選清單的一個選項，勾選狀態由自己保存。</summary>
public partial class TagFilterOption : ObservableObject
{
    private readonly Action _onChanged;

    public TagFilterOption(TagItem tag, bool isSelected, Action onChanged)
    {
        Name = tag.Name;
        Label = $"{tag.Name} ({tag.WorkCount})";
        // 設欄位不設屬性：不觸發 OnIsSelectedChanged，重建清單時才不會每個選項都重新查詢一次
        _isSelected = isSelected;
        _onChanged = onChanged;
    }

    public string Name { get; }
    public string Label { get; }

    [ObservableProperty] private bool _isSelected;

    // 由 MainViewModel 傳入重新查詢的動作，選項不必認識 MainViewModel
    partial void OnIsSelectedChanged(bool value) => _onChanged();
}