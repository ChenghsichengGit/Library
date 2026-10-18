namespace Library.Application.Dtos;

/// <summary>作者或社團清單的項目，WorkCount 是以這個角色參與、未刪除的作品數。</summary>
public record CreatorDto(int Id, string Name, int WorkCount);
