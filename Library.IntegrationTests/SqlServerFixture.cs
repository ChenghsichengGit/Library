using Testcontainers.MsSql;

namespace Library.IntegrationTests;

/// <summary>
/// 整組整合測試共用的 SQL Server：所有測試開始前啟動一個 Docker 容器，全部結束後自動刪除。
/// </summary>
/// <remarks>
/// 啟動容器要十幾秒，不能每個測試都開一次，所以用 Fixture 共用（像測試前只載入一次場景）。
/// 密碼和 port 由 Testcontainers 自動產生，不需要任何設定，也不會和開發用的 1433 衝突。
/// 執行整合測試前，Docker Desktop 要開著。
/// </remarks>
public class SqlServerFixture : IAsyncLifetime
{
    // 映像檔和 docker-compose.yml 用的是同一個，版本明確寫出來，避免套件升級時悄悄換版本
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // 指向容器裡的 SQL Server；各測試再在上面換成自己的資料庫名稱
    public string ConnectionString => _container.GetConnectionString();

    // 所有測試開始前：啟動容器
    public Task InitializeAsync() => _container.StartAsync();

    // 所有測試結束後：刪除容器
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
