using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Application.Exceptions;
using Library.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Works;

/// <summary>
/// 作品的關聯資料：作者與社團、類型、標籤、系列的存檔與修改。
/// </summary>
[Collection("SqlServer")]
public class WorkRelationTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    // 防：GetByIdAsync 漏了 Include／ThenInclude（作者變成空陣列或 NullReferenceException）、名字沒整理、沒排序
    [Fact]
    public async Task CreateAsync_SavesAuthorsAndCircles()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            Authors = ["B作者", " A作者 ", "", "a作者"],
            Circles = ["某社團"],
            WorkTypeId = 1
        });

        var result = await GetWorkAsync(id);

        // 空字串濾掉、去前後空白、a作者 和 A作者 視為同一個、依名字排序
        Assert.Equal(["A作者", "B作者"], result!.Authors);
        Assert.Equal(["某社團"], result.Circles);
    }

    // 防：UpdateAsync 漏了 Include，舊的關聯清不掉，結果變成 A 和 B 都在
    [Fact]
    public async Task UpdateAsync_ReplacesAuthors()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "テスト", Authors = ["A"], WorkTypeId = 1 });

        // PUT 是整筆取代，名稱也要給，否則會撞上 CHECK 約束
        Assert.True(await UpdateWorkAsync(id, new SaveWorkRequest { TitleJa = "テスト", Authors = ["B"], WorkTypeId = 1 }));

        Assert.Equal(["B"], (await GetWorkAsync(id))!.Authors);
    }

    // 防：新建的 Creator 沒放回 Dictionary，同一個新名字被建立兩次而撞上唯一索引
    [Fact]
    public async Task CreateAsync_SameNameAsAuthorAndCircle_CreatesOneCreator()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            Authors = ["Valve"],
            Circles = ["Valve"],
            WorkTypeId = 1
        });

        await using var verifyDb = CreateDbContext();
        Assert.Single(await verifyDb.Creators.ToListAsync());

        var result = await GetWorkAsync(id);
        Assert.Equal(["Valve"], result!.Authors);
        Assert.Equal(["Valve"], result.Circles);
    }

    // 防：GetByIdAsync 漏了 Include(WorkType)、WorkDto 對錯欄位
    [Fact]
    public async Task CreateAsync_SavesWorkType()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "A", WorkTypeId = 2 });

        var result = await GetWorkAsync(id);

        Assert.Equal("動畫", result!.WorkType);
        Assert.Equal(2, result.WorkTypeId);
    }

    // 防：ApplyAsync 讀了 work.WorkTypeId（舊值）而不是 request 的，修改後類型沒變
    [Fact]
    public async Task UpdateAsync_ChangesWorkType()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "A", WorkTypeId = 1 });

        await UpdateWorkAsync(id, new SaveWorkRequest { TitleEn = "A", WorkTypeId = 2 });

        Assert.Equal(2, (await GetWorkAsync(id))!.WorkTypeId);
    }

    // 防：拿掉類型檢查，存檔時撞上外鍵變成 DbUpdateException（500）
    [Fact]
    public async Task CreateAsync_UnknownWorkType_Throws()
    {
        var ex = await Assert.ThrowsAsync<WorkTypeNotFoundException>(
            () => CreateWorkAsync(new SaveWorkRequest { TitleJa = "A", WorkTypeId = 999 }));

        Assert.Equal(999, ex.WorkTypeId);
    }

    // 防：GetByIdAsync 漏了 Include(Tags)、名稱沒整理、沒排序
    [Fact]
    public async Task CreateAsync_SavesTags()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Tags = ["B標籤", " A標籤 ", "", "a標籤"]
        });

        // 空字串濾掉、去前後空白、a標籤 和 A標籤 視為同一個、依名字排序
        Assert.Equal(["A標籤", "B標籤"], (await GetWorkAsync(id))!.Tags);
    }

    // 防：UpdateAsync 漏了 Include，舊的關聯清不掉，結果變成 A 和 B 都在
    [Fact]
    public async Task UpdateAsync_ReplacesTags()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "テスト", Tags = ["A"], WorkTypeId = 1 });

        Assert.True(await UpdateWorkAsync(id, new SaveWorkRequest { TitleJa = "テスト", Tags = ["B"], WorkTypeId = 1 }));

        Assert.Equal(["B"], (await GetWorkAsync(id))!.Tags);
    }

    // 防：Dictionary 沒設不分大小寫，資料庫查得到 ASMR，但用 asmr 找不到而又新建一筆，撞上唯一索引
    [Fact]
    public async Task CreateAsync_ExistingTagName_ReusesTag()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "A", Tags = ["ASMR"], WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "B", Tags = ["asmr"], WorkTypeId = 1 });

        // Tags 表只能有一筆
        await using var verifyDb = CreateDbContext();
        Assert.Single(await verifyDb.Tags.ToListAsync());
    }

    // 防：GetByIdAsync 漏了 Include(Series)、名稱沒去空白、同名（不分大小寫）又建立一個系列
    [Fact]
    public async Task CreateAsync_SavesSeries_AndReusesExisting()
    {
        var firstId = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "A", Series = " Series ", WorkTypeId = 1 });
        var secondId = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "B", Series = "series", WorkTypeId = 1 });

        var first = await GetWorkAsync(firstId);
        var second = await GetWorkAsync(secondId);

        Assert.Equal("Series", first!.Series);
        Assert.Equal(first.SeriesId, second!.SeriesId);
        await using var verifyDb = CreateDbContext();
        Assert.Single(await verifyDb.Series.ToListAsync());
    }

    // 防：UpdateAsync 漏了 Include(Series)，設成 null 時 EF Core 看不出變化，系列清不掉
    [Fact]
    public async Task UpdateAsync_EmptySeries_ClearsSeries()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "A", Series = "S", WorkTypeId = 1 });

        Assert.True(await UpdateWorkAsync(id, new SaveWorkRequest { TitleJa = "A", Series = " ", WorkTypeId = 1 }));

        var result = await GetWorkAsync(id);
        Assert.Null(result!.SeriesId);
        Assert.Null(result.Series);
    }
}
