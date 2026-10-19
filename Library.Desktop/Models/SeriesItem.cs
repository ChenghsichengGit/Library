namespace Library.Desktop.Models;

/// <summary>對應 API 回傳的 SeriesDto。</summary>
public record SeriesItem(
    int Id,
    string Name,
    int WorkCount,
    string[] Authors,
    string Note = "",
    string? CoverKey = null,
    string? CoverUrl = null)
{
    /// <summary>建議清單右邊的說明，例如「Valve・2」。</summary>
    public string Detail => Authors.Length > 0 ? $"{string.Join("、", Authors)}・{WorkCount}" : $"{WorkCount}";
}
