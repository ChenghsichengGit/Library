namespace Library.Application.Dtos.Responses;

/// <summary>
/// API 回傳的一個 DLC。順序由 WorkDto.Dlcs 的陣列順序表示。
/// </summary>
public record WorkDlcDto(string Name, string Url, bool Purchased, decimal? Price, string Currency);
