namespace Library.Desktop.Services;

/// <summary>
/// API 回報使用者可以修正的錯誤（400 等）時由 WorksApiClient 丟出，帶著後端的訊息。
/// ViewModel 接住後顯示 Messages，不需要知道 HTTP 狀態碼。
/// </summary>
public class ApiValidationException(IReadOnlyList<string> messages)
    : Exception(string.Join("\n", messages))
{
    public IReadOnlyList<string> Messages { get; } = messages;
}
