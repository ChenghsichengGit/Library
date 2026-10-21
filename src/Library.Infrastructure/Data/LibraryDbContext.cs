using Library.Application.Abstractions;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Data;

/// <summary>
/// EF Core 的 DbContext，實作 ILibraryDbContext。登記成 Scoped，每個 HTTP 請求一個。
/// </summary>
public class LibraryDbContext : DbContext, ILibraryDbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Work> Works => Set<Work>();

    public DbSet<Creator> Creators => Set<Creator>();

    public DbSet<WorkType> WorkTypes => Set<WorkType>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Series> Series => Set<Series>();

    /// <summary>
    /// 慣例和屬性標記寫不出來的資料庫設定。修改結構後要產生 Migration。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 三種名稱至少一個不是空字串；就算程式有 bug 也寫不進無名資料
        modelBuilder.Entity<Work>().ToTable(t =>
            t.HasCheckConstraint("CK_Works_HasTitle",
                "[TitleZh] <> N'' OR [TitleJa] <> N'' OR [TitleEn] <> N''"));

        // 軟刪除：所有查詢自動排除已刪除的作品，要查已刪除的用 IgnoreQueryFilters()
        modelBuilder.Entity<Work>().HasQueryFilter(w => !w.DeletedAt.HasValue);

        // 主要名稱由資料庫計算並存起來，才能用在排序和查詢裡
        modelBuilder.Entity<Work>()
            .Property(w => w.Title)
            .HasComputedColumnSql("COALESCE(NULLIF([TitleZh], N''), NULLIF([TitleJa], N''), [TitleEn])", stored: true);

        // 複合主鍵包含 Role：同一個人在同一部作品可以同時是作者和社團
        modelBuilder.Entity<WorkCreator>().ToTable("WorkCreators")
            .HasKey(wc => new { wc.WorkId, wc.CreatorId, wc.Role });

        // 和 Work 的軟刪除配對：從 creator.Works 方向查時也看不到已刪除作品
        modelBuilder.Entity<WorkCreator>().HasQueryFilter(wc => wc.Work.DeletedAt == null);

        // SQL Server 預設不分大小寫，Valve 和 valve 視為同一個
        modelBuilder.Entity<Creator>().HasIndex(c => c.Name).IsUnique();

        modelBuilder.Entity<WorkType>().HasIndex(t => t.Name).IsUnique();

        // 外鍵由命名慣例推出；預設 Cascade 會連作品一起刪，改成 Restrict
        modelBuilder.Entity<Work>()
            .HasOne(w => w.WorkType)
            .WithMany(t => t.Works)
            .OnDelete(DeleteBehavior.Restrict);

        // 預設類型。已套用的種子資料不要再改：之後的 Migration 會產生 UPDATE／DELETE 覆蓋使用者的修改
        modelBuilder.Entity<WorkType>().HasData(
            new WorkType { Id = 1, Name = "漫畫", SortOrder = 0 },
            new WorkType { Id = 2, Name = "動畫", SortOrder = 1 },
            new WorkType { Id = 3, Name = "影片", SortOrder = 2 },
            new WorkType { Id = 4, Name = "遊戲", SortOrder = 3 },
            new WorkType { Id = WorkType.OtherId, Name = "其他", SortOrder = 99 }
        );

        modelBuilder.Entity<Tag>().HasIndex(t => t.Name).IsUnique();

        // 多對多不建中間類別，中間表 WorkTags 由 EF 管理
        modelBuilder.Entity<Work>()
            .HasMany(w => w.Tags)
            .WithMany(t => t.Works)
            .UsingEntity(j => j.ToTable("WorkTags"));

        modelBuilder.Entity<Series>().HasIndex(s => s.Name).IsUnique();

        // 刪除系列時由資料庫把作品的 SeriesId 設成 null；預設的 ClientSetNull 只處理有載入的作品
        modelBuilder.Entity<Work>()
            .HasOne(w => w.Series)
            .WithMany(s => s.Works)
            .OnDelete(DeleteBehavior.SetNull);

        // 網址：沒有 DbSet，表名要自己指定；必填外鍵預設 Cascade，刪除作品時一起刪
        modelBuilder.Entity<WorkUrl>().ToTable("WorkUrls");
        // 精度寫在這裡而不是 Entity 的 [Precision]，Domain 才不必依賴 EF Core
        modelBuilder.Entity<WorkUrl>().Property(u => u.Price).HasPrecision(12, 2);
        modelBuilder.Entity<WorkUrl>().Property(u => u.ListPrice).HasPrecision(12, 2);
        // 存名稱，資料庫看得懂，日後調整 enum 順序也不影響舊資料
        modelBuilder.Entity<WorkUrl>().Property(u => u.Language).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<WorkUrl>().HasQueryFilter(wu => wu.Work.DeletedAt == null);

        // DLC：設定同網址（沒有語言欄位）
        modelBuilder.Entity<WorkDlc>().ToTable("WorkDlcs");
        modelBuilder.Entity<WorkDlc>().Property(d => d.Price).HasPrecision(12, 2);
        modelBuilder.Entity<WorkDlc>().HasQueryFilter(wd => wd.Work.DeletedAt == null);
    }

    // 所有 DateTime 讀出時標記為 UTC，JSON 才會帶 Z
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
