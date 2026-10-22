using Library.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Respawn;
using Testcontainers.MsSql;

namespace Library.IntegrationTests.Support;

/// <summary>
/// 整組整合測試共用的 SQL Server 容器與測試資料庫：全部測試開始前啟動並 Migrate 一次，結束後刪除。需要 Docker Desktop。
/// </summary>
/// <remarks>
/// 啟動容器要十幾秒、從頭跑 Migration 每次一兩秒，所以都只做一次；每個測試開始前用 ResetAsync 清空資料。
/// port 由 Testcontainers 隨機分配，不會和開發用的 1433 衝突；密碼用它的預設值（容器只在測試期間存在）。
/// </remarks>
public class SqlServerFixture : IAsyncLifetime
{
    // 和 docker-compose.yml 同一個映像檔，版本明確寫出來
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private Respawner _respawner = null!;

    // 每張有種子資料的表：一句 INSERT 和它的參數值；InitializeAsync 準備好，ResetAsync 每次照著重放
    private readonly List<(string Sql, object?[] Values)> _seedInserts = [];

    /// <summary>測試資料庫的連線字串（容器裡的 LibraryTest，不是預設的 master）。</summary>
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "LibraryTest"
        }.ConnectionString;

        // 正式的 Migration 只跑這一次
        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
            PrepareSeedInserts(db);
        }

        // Migration 的紀錄不能清：清掉的話下次 Migrate 會以為資料表還沒建
        _respawner = await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = ["__EFMigrationsHistory"],
            // Id 從頭算，和全新的資料庫一樣
            WithReseed = true
        });
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // 查詢設定要和正式環境（Infrastructure 的 AddDbContext）一致
    public LibraryDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options);

    /// <summary>清空所有資料表，再放回 HasData 的種子資料（類型），回到剛 Migrate 完的狀態。</summary>
    public async Task ResetAsync()
    {
        await _respawner.ResetAsync(ConnectionString);

        await using var db = CreateDbContext();
        foreach (var (sql, values) in _seedInserts)
        {
            // SqlParameter 只能屬於一個指令，每次重新建立
            var parameters = values.Select((v, i) => (object)new SqlParameter($"@p{i}", v ?? DBNull.Value)).ToArray();
            await db.Database.ExecuteSqlRawAsync(sql, parameters);
        }
    }

    // 種子資料只存在設計時的模型（Migration 用的那份），執行時的模型是精簡過的，沒有 HasData
    private void PrepareSeedInserts(LibraryDbContext db)
    {
        var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var entityType in model.GetEntityTypes())
        {
            var rows = entityType.GetSeedData().ToList();
            if (rows.Count == 0)
                continue;

            // 種子資料有指定 Id，自動編號的欄位要先打開 IDENTITY_INSERT；三句放在同一批送出，才是同一個連線
            // 種子資料的字典也有導覽屬性（WorkType.Works），只取對應到欄位的屬性
            var table = entityType.GetTableName();
            var columns = entityType.GetProperties().Where(prop => rows[0].ContainsKey(prop.Name)).ToList();
            var placeholders = rows.Select((_, r) =>
                "(" + string.Join(", ", columns.Select((_, c) => $"@p{r * columns.Count + c}")) + ")");
            var sql = $"SET IDENTITY_INSERT [{table}] ON; " +
                      $"INSERT INTO [{table}] ({string.Join(", ", columns.Select(c => $"[{c.GetColumnName()}]"))}) " +
                      $"VALUES {string.Join(", ", placeholders)}; " +
                      $"SET IDENTITY_INSERT [{table}] OFF;";
            _seedInserts.Add((sql, rows.SelectMany(row => columns.Select(c => row[c.Name])).ToArray()));
        }
    }
}
