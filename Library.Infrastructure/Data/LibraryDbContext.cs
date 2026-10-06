using Library.Application.Abstractions;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Data;

/// <summary>
/// 和資料庫溝通的窗口（EF Core）。實作 Application 的 ILibraryDbContext。
/// </summary>
/// <remarks>
/// 負責：知道有哪些表、把 LINQ 翻譯成 SQL、記住查出來的物件被改了什麼（追蹤）、SaveChanges 時寫回資料庫。
/// 不是執行緒安全的，所以登記成 Scoped：每個 HTTP 請求各用一個。
/// </remarks>
public class LibraryDbContext : DbContext, ILibraryDbContext
{
    // options 裡有「連哪個資料庫、用什麼驅動」，由 AddInfrastructure 設定好再透過 DI 傳進來
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    /// <summary>Works 資料表。</summary>
    public DbSet<Work> Works => Set<Work>();

    /// <summary>
    /// Work 類別上寫不出來的資料庫設定放在這裡。修改後要跑 Migration（全域過濾器除外）。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CHECK 約束：三種名稱至少一個不是空字串。資料庫的最後一道防線，就算程式有 bug 也寫不進無名資料
        modelBuilder.Entity<Work>().ToTable(t =>
            t.HasCheckConstraint("CK_Works_HasTitle",
                "[TitleZh] <> N'' OR [TitleJa] <> N'' OR [TitleEn] <> N''"));

        // 全域查詢過濾器：所有查 Works 的查詢都自動加上「沒被刪除」，不用每個地方自己記得加
        // 要查已刪除的資料時，在查詢加 .IgnoreQueryFilters()
        modelBuilder.Entity<Work>().HasQueryFilter(w => !w.DeletedAt.HasValue);
    }
}
