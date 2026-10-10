using Testcontainers.MsSql;

namespace Library.IntegrationTests;

/// <summary>
/// 整組整合測試共用的 SQL Server 容器：全部測試開始前啟動，結束後刪除。需要 Docker Desktop。
/// </summary>
/// <remarks>
/// 啟動容器要十幾秒，所以共用一個。密碼和 port 由 Testcontainers 自動產生，不會和開發用的 1433 衝突。
/// </remarks>
public class SqlServerFixture : IAsyncLifetime
{
    // 和 docker-compose.yml 同一個映像檔，版本明確寫出來
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
