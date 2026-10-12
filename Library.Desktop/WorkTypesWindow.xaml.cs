using System.Windows;
using Library.Desktop.ViewModels;

namespace Library.Desktop;

/// <summary>
/// 類型管理視窗。ViewModel 由開啟它的一方（DialogService）設定到 DataContext。
/// </summary>
public partial class WorkTypesWindow : Window
{
    public WorkTypesWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ((WorkTypesViewModel)DataContext).LoadCommand.ExecuteAsync(null);
    }
}
