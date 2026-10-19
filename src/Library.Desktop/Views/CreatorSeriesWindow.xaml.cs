using Library.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Library.Desktop.Views;

/// <summary>
/// 作者／社團、系列的管理視窗。ViewModel 由開啟它的一方（DialogService）設定到 DataContext。
/// </summary>
public partial class CreatorSeriesWindow : FluentWindow
{
    public CreatorSeriesWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ((CreatorSeriesViewModel)DataContext).LoadCommand.ExecuteAsync(null);
    }
}
