namespace Library.Desktop.Services;

/// <summary>
/// 對話框的介面。ViewModel 透過它詢問使用者，不直接依賴 MessageBox，測試時可以換成假實作。
/// </summary>
public interface IDialogService
{
    /// <summary>顯示「是／否」確認視窗，按「是」回傳 true。</summary>
    bool Confirm(string message);
}
