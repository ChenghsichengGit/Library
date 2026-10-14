namespace Library.Application.Dtos;

/// <summary>標籤清單的項目，WorkCount 是使用中（未刪除）的作品數。</summary>
public record TagDto(int Id, string Name, int WorkCount);