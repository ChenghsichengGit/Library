using System.Windows;
using Library.Desktop.Services;
using Library.Desktop.ViewModels;

namespace Library.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel(new WorksApiClient(), new MessageBoxDialogService());
        DataContext = viewModel;

        // 視窗打開時自動載入一次，不用先按按鈕
        Loaded += async (_, _) => await viewModel.LoadCommand.ExecuteAsync(null);
    }
}