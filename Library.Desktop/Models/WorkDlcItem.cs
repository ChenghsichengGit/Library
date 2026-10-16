namespace Library.Desktop.Models;

/// <summary>作品的一個 DLC，對應 API 的 WorkDlcDto；送出時也用同一個格式。</summary>
public record WorkDlcItem(string Name, string Url, bool Purchased, decimal? Price, string Currency);
