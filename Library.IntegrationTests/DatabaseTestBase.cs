using Library.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests;

/// <summary>
/// 整合測試的基底類別：每個測試用一個新資料庫，以正式的 Migration 建表，結束後刪除。
/// </summary>
/// <remarks>
/// 子類別仍要自己標上 [Collection("SqlServer")]：xUnit 2 不會從父類別繼承這個標記。
/// </remarks>
public abstract class DatabaseTestBase : IAsyncLifetime
{
    private readonly string _connectionString;

    protected DatabaseTestBase(SqlServerFixture fixture)
    {
        _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"LibraryTest_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    // 查詢設定要和正式環境（Infrastructure 的 AddDbContext）一致
    protected LibraryDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlServer(_connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options);


    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }
}