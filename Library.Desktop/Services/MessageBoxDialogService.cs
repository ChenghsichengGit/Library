using System.Windows;

namespace Library.Desktop.Services;

/// <summary>
/// IDialogService 的正式實作：用 WPF 的 MessageBox 跳出視窗。在 MainWindow.xaml.cs 建立並傳給 ViewModel。
/// </summary>
public class MessageBoxDialogService : IDialogService
{
    public bool Confirm(string message)
    {
        var result = MessageBox.Show(message, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}
