namespace Library.Desktop.Services;

/// <summary>
/// 對話框與子視窗的介面。ViewModel 透過它和使用者互動，不直接依賴 WPF 的視窗元件，測試時可以換成假實作。
/// </summary>
public interface IDialogService
{
    /// <summary>顯示「是／否」確認視窗，按「是」回傳 true。</summary>
    bool Confirm(string message);

    /// <summary>開啟類型管理視窗，關閉之前不能操作主視窗。</summary>
    void ShowWorkTypeManager();

    /// <summary>開啟選擇圖片的對話框，回傳檔案路徑；按取消回傳 null。</summary>
    string? PickImageFile();

    /// <summary>用預設瀏覽器開啟網址。</summary>
    void OpenInBrowser(Uri url);

    /// <summary>開啟作者／社團、系列的管理視窗；selectCreatorId 有值時先選好那個人。</summary>
    void ShowCreatorSeriesManager(int? selectCreatorId);
}
