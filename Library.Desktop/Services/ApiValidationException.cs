namespace Library.Desktop.Services;

/// <summary>
/// API 回 400（驗證失敗）時，由 WorksApiClient 丟出，帶著後端的錯誤訊息。
/// ViewModel 接住後把 Messages 顯示在畫面上，不需要知道 HTTP 狀態碼。
/// </summary>
/// <remarks>類別名稱後面的括號是「主要建構子」（C# 12），等於寫了一個接收 messages 的建構子。</remarks>
public class ApiValidationException(IReadOnlyList<string> messages)
    : Exception(string.Join("\n", messages))
{
    public IReadOnlyList<string> Messages { get; } = messages;
}
