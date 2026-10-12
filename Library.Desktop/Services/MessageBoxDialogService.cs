using System.Windows;
using Library.Desktop.ViewModels;

namespace Library.Desktop.Services;

/// <summary>
/// IDialogService 的正式實作：MessageBox 與子視窗。
/// </summary>
public class MessageBoxDialogService : IDialogService
{
    public bool Confirm(string message)
    {
        var result = MessageBox.Show(message, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public void ShowWorkTypeManager()
    {
        var window = new WorkTypesWindow
        {
            DataContext = new WorkTypesViewModel(new WorksApiClient(), this),
            Owner = Application.Current.MainWindow
        };
        // ShowDialog 會等視窗關閉才返回，呼叫端接著重新讀取資料
        window.ShowDialog();
    }
}
