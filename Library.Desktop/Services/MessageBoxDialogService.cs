using System.Windows;

namespace Library.Desktop.Services;

public class MessageBoxDialogService : IDialogService
{
    public bool Confirm(string message)
    {
        var result = MessageBox.Show(message, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}