namespace Library.Desktop.Services;

// API 回 400 時丟出，帶著後端的驗證訊息，讓畫面可以顯示給使用者
public class ApiValidationException(IReadOnlyList<string> messages)
    : Exception(string.Join("\n", messages))
{
    public IReadOnlyList<string> Messages { get; } = messages;
}