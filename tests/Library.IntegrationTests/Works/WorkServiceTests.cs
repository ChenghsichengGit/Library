using Library.Application.Dtos.Requests;
using Library.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Works;

/// <summary>
/// WorkService 基本的新增、讀取、修改、軟刪除，以及資料庫計算的主要名稱。
/// </summary>
[Collection("SqlServer")]
public class WorkServiceTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    [Fact]
    public async Task CreateAsync_SetsCreatedAtToCurrentTime()
    {
        await using var db = CreateDbContext();

        var dto = await CreateService(db).CreateAsync(new SaveWorkRequest { TitleJa = "テスト", WorkTypeId = 1 });

        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), dto.CreatedAt);
        Assert.Equal(dto.CreatedAt, dto.UpdatedAt);
    }

    [Fact]
    public async Task CreateAsync_TrimsTitlesAndSaves()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "  テスト  ", WorkTypeId = 1 });

        await using var verifyDb = CreateDbContext();
        var saved = await verifyDb.Works.SingleAsync(w => w.Id == id);

        Assert.Equal("テスト", saved.TitleJa);
    }

    // 主要名稱由資料庫的計算欄位產生，只能用整合測試驗證；三個都空的作品會被 CHECK 約束擋下
    [Theory]
    [InlineData("中文", "日本語", "English", "中文")]
    [InlineData("", "日本語", "English", "日本語")]
    [InlineData("", "", "English", "English")]
    public async Task CreateAsync_SetsTitleFromFirstNonEmptyName(string zh, string ja, string en, string expected)
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleZh = zh, TitleJa = ja, TitleEn = en, WorkTypeId = 1 });

        var result = await GetWorkAsync(id);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        Assert.Null(await GetWorkAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await UpdateWorkAsync(999, new SaveWorkRequest { TitleJa = "A" }));
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        await using var db = CreateDbContext();

        Assert.False(await CreateService(db).DeleteAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesAllFields_AndKeepsCreatedAt()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            ReleaseDate = new DateOnly(2025, 10, 10),
            Score = 5,
            WorkTypeId = 1
        });
        // 時間往前一天：建立時間如果被改掉會變成 01-02
        Time.Advance(TimeSpan.FromDays(1));

        await UpdateWorkAsync(id, new SaveWorkRequest { TitleEn = "TEST", Score = 3, WorkTypeId = 1 });

        // 沒送的欄位被清掉、建立時間保留、修改時間更新
        await using var verifyDb = CreateDbContext();
        var result = await verifyDb.Works.SingleAsync(w => w.Id == id);

        Assert.Empty(result.TitleJa);
        Assert.Equal("TEST", result.TitleEn);
        Assert.Null(result.ReleaseDate);
        Assert.Equal(3, result.Score);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), result.CreatedAt);
        Assert.Equal(new DateTime(2026, 1, 2, 12, 0, 0), result.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "  テスト  ", WorkTypeId = 1 });
        Time.Advance(TimeSpan.FromDays(1));

        await using (var db = CreateDbContext())
        {
            Assert.True(await CreateService(db).DeleteAsync(id));
        }

        // 一般查詢查不到，關掉全域過濾器才查得到：資料還在，只是被標記
        await using var verifyDb = CreateDbContext();
        var result = await verifyDb.Works.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id);

        Assert.Null(await GetWorkAsync(id));
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 1, 2, 12, 0, 0), result.DeletedAt);
        Assert.Equal(result.DeletedAt, result.UpdatedAt);
    }
}
