using System.Windows;
using Library.Desktop.Services;
using Library.Desktop.ViewModels;

namespace Library.Desktop;

/// <summary>
/// 主視窗的後端程式碼（code-behind）。只負責把畫面和 ViewModel 接起來，不放任何邏輯。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        // 依照 MainWindow.xaml 建立畫面上的元件
        InitializeComponent();

        // 在這裡決定要用哪些實作：正式執行用真的 API 和真的 MessageBox，測試時可以換成假的
        var viewModel = new MainViewModel(new WorksApiClient(), new MessageBoxDialogService());

        // XAML 裡所有的 {Binding 名稱}，都會去 DataContext（這個 ViewModel）身上找同名的屬性
        DataContext = viewModel;

        // 視窗打開時自動載入一次，不用先按按鈕
        Loaded += async (_, _) => await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
