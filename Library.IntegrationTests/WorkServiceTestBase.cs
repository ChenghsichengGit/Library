using Library.Application.Dtos;
using Library.Application.Services;
using Library.Infrastructure.Data;
using Microsoft.Extensions.Time.Testing;

namespace Library.IntegrationTests;

/// <summary>
/// WorkService 整合測試的共用基底：假時鐘、假封面儲存與「建立、修改、讀取」的 helper。
/// </summary>
/// <remarks>
/// 每個 helper 各開一個新的 DbContext，驗證時才不會讀到追蹤中的舊資料（抓得到忘了 SaveChangesAsync、漏了 Include）。
/// WorkTypeId = 1 是種子資料「漫畫」。不經過 Controller，所以沒有 [ApiController] 的驗證。
/// </remarks>
public abstract class WorkServiceTestBase(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    // 時間固定在 2026-01-01 12:00 UTC，斷言才能精確比對；Advance 才會往前
    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    // 所有 helper 共用，測試才查得到刪了哪些封面
    protected FakeCoverStorage Covers { get; } = new();

    protected WorkService CreateService(LibraryDbContext db) => new(db, Covers, new CoverCleaner(db, Covers), Time);

    /// <summary>新增一筆作品，回傳 Id。</summary>
    protected async Task<int> CreateWorkAsync(SaveWorkRequest request)
    {
        await using var db = CreateDbContext();
        return (await CreateService(db).CreateAsync(request)).Id;
    }

    /// <summary>依序新增多筆作品（同一個時間點）。</summary>
    protected async Task CreateWorksAsync(params SaveWorkRequest[] requests)
    {
        foreach (var request in requests)
            await CreateWorkAsync(request);
    }

    protected async Task<bool> UpdateWorkAsync(int id, SaveWorkRequest request)
    {
        await using var db = CreateDbContext();
        return await CreateService(db).UpdateAsync(id, request);
    }

    protected async Task<WorkDto?> GetWorkAsync(int id)
    {
        await using var db = CreateDbContext();
        return await CreateService(db).GetByIdAsync(id);
    }

    protected async Task<List<WorkDto>> GetWorksAsync(WorkQuery query)
    {
        await using var db = CreateDbContext();
        return await CreateService(db).GetWorksAsync(query);
    }
}
