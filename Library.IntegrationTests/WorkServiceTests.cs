using Library.Application.Dtos;
using Library.Application.Services;
using Library.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Library.IntegrationTests;

public class WorkServiceTests : IAsyncLifetime
{
    // 每個測試各用一個隨機命名的資料庫，測試之間不會互相影響
    private readonly string _connectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database=LibraryTest_{Guid.NewGuid():N};Trusted_Connection=True;";

    // 假時鐘固定在 2026-01-01 12:00 UTC，時間相關的斷言才能精確比對
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private LibraryDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LibraryDbContext>().UseSqlServer(_connectionString).Options);

    private WorkService CreateService(LibraryDbContext db) => new(db, _time);

    // 每個測試開始前：建立資料庫，並用正式的 Migration 建表
    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    // 每個測試結束後：刪除資料庫
    public async Task DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    // ↓ 測試寫在這裡
    [Fact]
    public async Task CreateAsync_SetsCreatedAtToCurrentTime()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var dto = await service.CreateAsync(new SaveWorkRequest { TitleJa = "テスト" });

        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), dto.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_TrimsTitlesAndSaves()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest { TitleJa = "  テスト  " });
            id = dto.Id;
        }

        // 用新的 DbContext 讀，確保讀到的是資料庫裡真正存的值，而不是記憶體裡的物件
        await using var verifyDb = CreateDbContext();
        var saved = await verifyDb.Works.SingleAsync(w => w.Id == id);

        Assert.Equal("テスト", saved.TitleJa);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFalse()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.UpdateAsync(999, new SaveWorkRequest { TitleJa = "A" });

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.DeleteAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest { TitleJa = "  テスト  " });
            id = dto.Id;
        }

        _time.Advance(TimeSpan.FromDays(1));
        
        await using (var db2 = CreateDbContext())
        {
            var service1 = CreateService(db2);
            var success = await service1.DeleteAsync(id);
            Assert.True(success);
        }


        // 用新的 DbContext 讀，確保讀到的是資料庫裡真正存的值，而不是記憶體裡的物件
        await using var verifyDb = CreateDbContext();
        var service = CreateService(verifyDb);

        var found = await service.GetByIdAsync(id);
        var result = await verifyDb.Works.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id);

        Assert.Null(found);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 1, 2, 12, 0, 0), result.DeletedAt);
    }
}