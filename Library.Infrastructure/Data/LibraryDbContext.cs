using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Data;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Work> Works => Set<Work>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Work>().ToTable(t =>
            t.HasCheckConstraint("CK_Works_HasTitle",
                "[TitleZh] <> N'' OR [TitleJa] <> N'' OR [TitleEn] <> N''"));
        modelBuilder.Entity<Work>().HasQueryFilter(w => !w.DeletedAt.HasValue);
    }
}