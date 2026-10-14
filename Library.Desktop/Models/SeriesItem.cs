namespace Library.Desktop.Models;

/// <summary>對應 API 回傳的 SeriesDto。</summary>
public record SeriesItem(int Id, string Name, int WorkCount, string[] Authors);