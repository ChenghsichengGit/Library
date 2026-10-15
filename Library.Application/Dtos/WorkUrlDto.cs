using Library.Domain.Entities;

namespace Library.Application.Dtos;

/// <summary>
/// API 回傳的一個網址。順序由 WorkDto.Urls 的陣列順序表示，所以不含 Position。
/// </summary>
public record WorkUrlDto(string Url, bool Purchased, UrlLanguage? Language, decimal? Price, string Currency);
