using Library.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Library.Desktop.Views;

/// <summary>
/// 全庫重複掃描視窗。ViewModel 由開啟它的一方（DialogService）設定到 DataContext。
/// </summary>
public partial class DuplicatesWindow : FluentWindow
{
    public DuplicatesWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ((DuplicatesViewModel)DataContext).LoadCommand.ExecuteAsync(null);
    }
}
