using System.Windows.Controls;
using System.Windows.Input;
using Library.Desktop.Models;
using Library.Desktop.ViewModels.Parts;
using Library.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Library.Desktop.Controls;

/// <summary>
/// 多個名字的輸入元件（標籤塊 ＋ 自動完成）。DataContext 是 NameListEditor；AutoSuggestBox 的事件在這裡轉成命令。
/// </summary>
public partial class NameListInput : UserControl
{
    public NameListInput()
    {
        InitializeComponent();
    }

    private NameListEditor Editor => (NameListEditor)DataContext;

    // 從建議清單點選或用方向鍵選了一個
    private void Box_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is NameOption option)
            Editor.AddCommand.Execute(option.Name);
    }

    // 沒有選建議、直接按 Enter：用輸入框的文字
    private void Box_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        Editor.AddCommand.Execute(null);
    }

    // 建議由 NameListEditor 篩選；標記成已處理，WPF UI 才不會用它內建的篩選把 ItemsSource 換成空清單
    private void Box_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        args.Handled = true;
    }

    // 輸入框是空的時按 Backspace 刪除最後一個標籤塊；有文字時照常刪字
    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back && Editor.RemoveLastIfInputEmpty())
            e.Handled = true;
    }
}
