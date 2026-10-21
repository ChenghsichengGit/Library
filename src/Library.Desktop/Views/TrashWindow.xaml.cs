using Library.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Library.Desktop.Views;

/// <summary>
/// 最近刪除視窗。ViewModel 由開啟它的一方（DialogService）設定到 DataContext。
/// </summary>
public partial class TrashWindow : FluentWindow
{
    public TrashWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ((TrashViewModel)DataContext).LoadCommand.ExecuteAsync(null);
    }
}
