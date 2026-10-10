using System.Windows;

namespace Library.Desktop.Services;

/// <summary>
/// IDialogService 的正式實作：用 WPF 的 MessageBox。
/// </summary>
public class MessageBoxDialogService : IDialogService
{
    public bool Confirm(string message)
    {
        var result = MessageBox.Show(message, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}
