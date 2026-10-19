using Library.Application.Dtos;

namespace Library.IntegrationTests;

/// <summary>
/// WorkService.GetWorksAsync 的搜尋、篩選與排序。
/// </summary>
[Collection("SqlServer")]
public class WorkQueryTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    [Fact]
    public async Task GetWorksAsync_NoFilter_ReturnsAll()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "魔法少女", WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法", WorkTypeId = 1 },
            new SaveWorkRequest { TitleZh = "其他作品", WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery());

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetWorksAsync_FiltersBySearchText_InAnyTitleOrRemark()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "魔法少女", WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法", WorkTypeId = 1 },
            new SaveWorkRequest { TitleZh = "其他作品", WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { Q = "魔法" });

        // 第 1 筆符合日文名稱，第 2 筆符合備註
        Assert.Equal(2, result.Count);
    }

    // 作品 B 的名稱不含 Valve，被找到只可能是因為作者名稱符合
    [Fact]
    public async Task GetWorksAsync_FiltersBySearchText_InCreatorName()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "A", Authors = ["S"], Circles = ["C", "T"], WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "B", Authors = ["Valve"], WorkTypeId = 1 });

        var work = Assert.Single(await GetWorksAsync(new WorkQuery { Q = "Valve" }));

        Assert.Equal("B", work.Title);
    }

    // 防：搜尋沒比對系列名稱（作品名稱故意不含搜尋字）；沒有系列的作品讓查詢出錯
    [Fact]
    public async Task GetWorksAsync_FiltersBySearchText_InSeriesName()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "A", Series = "Half-Life", WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "B", WorkTypeId = 1 });

        var work = Assert.Single(await GetWorksAsync(new WorkQuery { Q = "Half" }));

        Assert.Equal("A", work.Title);
    }

    [Fact]
    public async Task GetWorksAsync_FiltersByScoreRange()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "魔法少女", Score = 0, WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法", Score = 3, WorkTypeId = 1 },
            new SaveWorkRequest { TitleZh = "其他作品", Score = 5, WorkTypeId = 1 },
            new SaveWorkRequest { TitleZh = "其他作品2", Score = 6, WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { MinScore = 3, MaxScore = 5 });

        // 條件寫反時（篩出 0 和 6）筆數也是 2，所以也檢查每筆的評分
        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.InRange(w.Score, 3, 5));
    }

    // 兩種類型的筆數不同（1 和 2），條件寫反時筆數就對不上
    [Fact]
    public async Task GetWorksAsync_FiltersByWorkType()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleEn = "TEST", WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "TEST1", WorkTypeId = 2 },
            new SaveWorkRequest { TitleEn = "TEST2", WorkTypeId = 2 });

        var result = await GetWorksAsync(new WorkQuery { WorkTypeId = 2 });

        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.Equal(2, w.WorkTypeId));
        Assert.Single(result, w => w.Title == "TEST1");
        Assert.Single(result, w => w.Title == "TEST2");
    }

    // 防：AND／OR 寫反、預設不是 AND、篩選清單沒整理（空字串、重複被當成條件）
    [Theory]
    [InlineData(null, new[] { "AB" })]
    [InlineData(TagMatch.Any, new[] { "A", "AB" })]
    public async Task GetWorksAsync_FiltersByTags(TagMatch? mode, string[] expected)
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleEn = "A", Tags = ["中文"], WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "AB", Tags = ["中文", "長篇"], WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "C", Tags = ["短篇"], WorkTypeId = 1 });

        // null 時不設定，測的是預設值
        var query = new WorkQuery { Tags = ["中文", " 長篇 ", "長篇", ""], Sort = WorkSort.Title };
        if (mode is not null)
            query.TagMode = mode.Value;

        var result = await GetWorksAsync(query);

        Assert.Equal(expected, result.Select(w => w.Title));
    }

    // 防：篩選失效（回傳全部）、條件寫反；沒有網址的作品算未購買
    [Theory]
    [InlineData(true, new[] { "一買一沒買" })]
    [InlineData(false, new[] { "都沒買", "沒有網址" })]
    public async Task GetWorksAsync_FiltersByPurchased(bool purchased, string[] expected)
    {
        await CreateWorksAsync(
            new SaveWorkRequest
            {
                TitleZh = "一買一沒買",
                WorkTypeId = 1,
                Urls =
                [
                    new SaveWorkUrlRequest { Url = "https://a.example.com", Purchased = true },
                    new SaveWorkUrlRequest { Url = "https://b.example.com" }
                ]
            },
            new SaveWorkRequest
            {
                TitleZh = "都沒買",
                WorkTypeId = 1,
                Urls = [new SaveWorkUrlRequest { Url = "https://c.example.com" }]
            },
            new SaveWorkRequest { TitleZh = "沒有網址", WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { Purchased = purchased });

        Assert.Equal(expected.Order(), result.Select(w => w.Title).Order());
    }

    [Fact]
    public async Task GetWorksAsync_SortsByTitle()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "C", WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "B", WorkTypeId = 1 },
            new SaveWorkRequest { TitleZh = "A", WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { Sort = WorkSort.Title, Desc = false });

        Assert.Equal(["A", "B", "C"], result.Select(w => w.Title));
    }

    // 降冪時沒有上架日的也在最後
    [Fact]
    public async Task GetWorksAsync_SortsByReleaseDate_NullsLast()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleZh = "A", ReleaseDate = new DateOnly(2025, 3, 10), WorkTypeId = 1 },
            new SaveWorkRequest { TitleEn = "B", WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "C", ReleaseDate = new DateOnly(2025, 1, 1), WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { Sort = WorkSort.ReleaseDate, Desc = true });

        Assert.Equal([new DateOnly(2025, 3, 10), new DateOnly(2025, 1, 1), null], result.Select(w => w.ReleaseDate));
    }

    // 防：修改時沒更新 UpdatedAt；依修改時間排序寫成依加入時間
    [Fact]
    public async Task GetWorksAsync_SortsByUpdatedAt()
    {
        var a = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "A", WorkTypeId = 1 });
        Time.Advance(TimeSpan.FromHours(1));
        await CreateWorkAsync(new SaveWorkRequest { TitleJa = "B", WorkTypeId = 1 });
        Time.Advance(TimeSpan.FromHours(1));
        await UpdateWorkAsync(a, new SaveWorkRequest { TitleJa = "A", WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { Sort = WorkSort.UpdatedAt, Desc = true });

        // 依加入時間會是 B、A
        Assert.Equal(["A", "B"], result.Select(w => w.Title));
    }

    // 防：評分相同時沒依修改時間排（Id、加入時間、修改時間升冪排出來都不一樣）；升冪、降冪都是最近改過的在前
    [Theory]
    [InlineData(true, new[] { "B", "C", "A", "D" })]
    [InlineData(false, new[] { "D", "B", "C", "A" })]
    public async Task GetWorksAsync_SortsByScore_ThenRecentlyUpdatedFirst(bool desc, string[] expected)
    {
        await CreateWorkAsync(new SaveWorkRequest { TitleJa = "A", Score = 5, WorkTypeId = 1 });
        Time.Advance(TimeSpan.FromHours(1));
        var b = await CreateWorkAsync(new SaveWorkRequest { TitleJa = "B", Score = 5, WorkTypeId = 1 });
        Time.Advance(TimeSpan.FromHours(1));
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "C", Score = 5, WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "D", Score = 3, WorkTypeId = 1 });
        Time.Advance(TimeSpan.FromHours(1));
        await UpdateWorkAsync(b, new SaveWorkRequest { TitleJa = "B", Score = 5, WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { Sort = WorkSort.Score, Desc = desc });

        Assert.Equal(expected, result.Select(w => w.Title));
    }

    // ZUN：紅魔郷、妖々夢當作者（東方），秘封當社團（沒系列）；東方二次是別人的東方作品；Portal 都不相干
    private async Task<(int Zun, int Touhou)> SeedCreatorAndSeriesAsync()
    {
        await CreateWorksAsync(
            new SaveWorkRequest { TitleJa = "紅魔郷", Authors = ["ZUN"], Series = "東方", WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "妖々夢", Authors = ["ZUN"], Series = "東方", WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "秘封", Circles = ["ZUN"], WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "東方二次", Authors = ["Other"], Series = "東方", WorkTypeId = 1 },
            new SaveWorkRequest { TitleJa = "Portal", Authors = ["Valve"], Series = "Portal", WorkTypeId = 1 });

        await using var db = CreateDbContext();
        return (db.Creators.Single(c => c.Name == "ZUN").Id, db.Series.Single(s => s.Name == "東方").Id);
    }

    // 防：只比對作者角色（漏了當社團的秘封）
    [Fact]
    public async Task GetWorksAsync_FiltersByCreatorId_AnyRole()
    {
        var (zun, _) = await SeedCreatorAndSeriesAsync();

        var result = await GetWorksAsync(new WorkQuery { CreatorId = zun });

        Assert.Equal(["妖々夢", "秘封", "紅魔郷"], result.Select(w => w.Title).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetWorksAsync_FiltersBySeriesId()
    {
        var (_, touhou) = await SeedCreatorAndSeriesAsync();

        var result = await GetWorksAsync(new WorkQuery { SeriesId = touhou });

        Assert.Equal(["妖々夢", "東方二次", "紅魔郷"], result.Select(w => w.Title).Order(StringComparer.Ordinal));
    }

    // 防：兩個條件只套用其中一個
    [Fact]
    public async Task GetWorksAsync_FiltersByCreatorAndSeries_Together()
    {
        var (zun, touhou) = await SeedCreatorAndSeriesAsync();

        var result = await GetWorksAsync(new WorkQuery { CreatorId = zun, SeriesId = touhou });

        Assert.Equal(["妖々夢", "紅魔郷"], result.Select(w => w.Title).Order(StringComparer.Ordinal));
    }
}
