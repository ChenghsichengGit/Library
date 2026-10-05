namespace Library.Desktop.Services;

// ViewModel 透過這個介面跳出對話框，不直接依賴 WPF 的 MessageBox，測試時可以換成假的實作
public interface IDialogService
{
    bool Confirm(string message);
}