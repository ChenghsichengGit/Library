using Library.Infrastructure.Data;

namespace Library.IntegrationTests.Support;

/// <summary>
/// 整合測試的基底類別：所有測試共用一個已 Migrate 的資料庫，每個測試開始前清空資料、放回種子資料。
/// </summary>
/// <remarks>
/// 子類別仍要自己標上 [Collection("SqlServer")]：xUnit 2 不會從父類別繼承這個標記。
/// 同一個 Collection 的測試依序執行，所以共用一個資料庫不會互相干擾。
/// </remarks>
public abstract class DatabaseTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    protected LibraryDbContext CreateDbContext() => fixture.CreateDbContext();

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
