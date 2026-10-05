using Testcontainers.MsSql;

namespace Library.IntegrationTests;

// 整組整合測試共用一個 SQL Server 容器：啟動要十幾秒，不能每個測試都開一次
public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // 指向容器裡的 SQL Server；各測試再在上面換成自己的資料庫名稱
    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}