using System.Windows;
using Library.Desktop.Services;
using Library.Desktop.ViewModels;

namespace Library.Desktop;

/// <summary>
/// 主視窗的 code-behind：只負責建立 ViewModel 並接上畫面，不放邏輯。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 在這裡決定實作：正式執行用真的 API 和 MessageBox
        var viewModel = new MainViewModel(new WorksApiClient(), new MessageBoxDialogService());
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
