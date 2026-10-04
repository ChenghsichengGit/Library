using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Abstractions;

public interface ILibraryDbContext
{
    DbSet<Work> Works { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}