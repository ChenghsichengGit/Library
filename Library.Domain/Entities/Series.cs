using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Entities;

/// <summary>
/// 系列：作品的分組，作品可以沒有系列。名稱唯一。系列的作者不另外存，由底下的作品推算。
/// </summary>
public class Series
{
    public int Id { get; set; }

    [MaxLength(300)]
    public string Name { get; set; } = "";

    public List<Work> Works { get; set; } = [];
}