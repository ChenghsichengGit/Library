using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Abstractions;

/// <summary>
/// Application 存取資料的介面，實作是 Infrastructure 的 LibraryDbContext。
/// </summary>
/// <remarks>
/// 直接暴露 DbSet 而不是每個查詢一個方法的 Repository，保留用 LINQ 自由組合條件的能力。
/// WorkCreator 沒有 DbSet，一律透過 work.Creators 操作。
/// </remarks>
public interface ILibraryDbContext
{
    DbSet<Work> Works { get; }

    DbSet<Creator> Creators { get; }

    DbSet<WorkType> WorkTypes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
