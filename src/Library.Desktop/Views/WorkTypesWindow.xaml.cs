using Library.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Library.Desktop.Views;

/// <summary>
/// 類型管理視窗。ViewModel 由開啟它的一方（DialogService）設定到 DataContext。
/// </summary>
public partial class WorkTypesWindow : FluentWindow
{
    public WorkTypesWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ((WorkTypesViewModel)DataContext).LoadCommand.ExecuteAsync(null);
    }
}
