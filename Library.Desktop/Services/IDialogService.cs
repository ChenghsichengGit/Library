namespace Library.Desktop.Services;

/// <summary>
/// 跳出對話框的介面。ViewModel 透過它詢問使用者，不直接呼叫 WPF 的 MessageBox。
/// </summary>
/// <remarks>
/// ViewModel 不應該依賴畫面元件：直接用 MessageBox 的話，測試 ViewModel 時會真的跳出視窗卡住。
/// 正式執行用 MessageBoxDialogService，測試時可以換成直接回傳 true／false 的假實作。
/// 和後端的 ILibraryDbContext、TimeProvider 是同一個概念。
/// </remarks>
public interface IDialogService
{
    /// <summary>顯示「是／否」確認視窗，使用者按「是」回傳 true。</summary>
    bool Confirm(string message);
}
