using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Application.Services;
using Library.IntegrationTests.Support;

namespace Library.IntegrationTests.Works;

/// <summary>
/// DuplicateService.CheckAsync：網址相同、同作者且名稱相同是完全重複，名稱相同但作者不同是可能重複。
/// </summary>
[Collection("SqlServer")]
public class WorkDuplicateTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    private const string SteamUrl = "https://store.steampowered.com/app/620";

    private async Task<DuplicateCheckDto> CheckAsync(CheckDuplicateRequest request)
    {
        await using var db = CreateDbContext();
        return await new DuplicateService(db).CheckAsync(request);
    }

    private static SaveWorkRequest Work(string titleJa, string? author = null, string? url = null) => new()
    {
        TitleJa = titleJa,
        WorkTypeId = 1,
        Authors = author is null ? [] : [author],
        Urls = url is null ? [] : [new SaveWorkUrlRequest { Url = url }]
    };

    // 防：網址沒正規化（追蹤參數、結尾斜線不同就對不上）、網址相同沒判成完全重複
    [Fact]
    public async Task CheckAsync_SameUrl_IsExact()
    {
        var id = await CreateWorkAsync(Work("Portal 2", "Valve", SteamUrl));

        var result = await CheckAsync(new CheckDuplicateRequest
        {
            TitleEn = "完全不同的名字",
            Urls = ["https://store.steampowered.com/app/620/?utm_source=x"]
        });

        Assert.Equal(DuplicateLevel.Exact, result.Level);
        var match = Assert.Single(result.Matches);
        Assert.Equal(id, match.Id);
        Assert.Equal(["網址相同"], match.Reasons);
    }

    // 防：作者沒比對（兩筆都變成同一級）、作者比對分大小寫、漢化標記沒去掉、完全重複沒排在前面
    [Fact]
    public async Task CheckAsync_SameTitle_ExactWithSameAuthor_SimilarOtherwise()
    {
        var other = await CreateWorkAsync(Work("[漢化] 魔法少女", "Beta"));
        var same = await CreateWorkAsync(Work("魔法少女", "Alpha"));

        var result = await CheckAsync(new CheckDuplicateRequest { TitleZh = "魔法少女", Authors = ["alpha"] });

        Assert.Equal(DuplicateLevel.Exact, result.Level);
        Assert.Equal([same, other], result.Matches.Select(m => m.Id));
        Assert.Equal(DuplicateLevel.Exact, result.Matches[0].Level);
        Assert.Equal(["同作者且作品名相同"], result.Matches[0].Reasons);
        Assert.Equal(DuplicateLevel.Similar, result.Matches[1].Level);
        Assert.Equal(["作品名相同"], result.Matches[1].Reasons);
    }

    // 防：只比對同一種語言的名稱（表單的中文名稱要能對上既有作品的日文名稱）、沒有作者時被當成同作者
    [Fact]
    public async Task CheckAsync_TitleInAnotherLanguage_IsMatched()
    {
        var id = await CreateWorkAsync(Work("ポータル２"));

        var result = await CheckAsync(new CheckDuplicateRequest { TitleZh = "ポータル2" });

        Assert.Equal(DuplicateLevel.Similar, result.Level);
        Assert.Equal(id, Assert.Single(result.Matches).Id);
    }

    // 防：續作被當成重複
    [Fact]
    public async Task CheckAsync_Sequel_IsNotDuplicate()
    {
        await CreateWorkAsync(Work("Portal", "Valve"));

        var result = await CheckAsync(new CheckDuplicateRequest { TitleEn = "Portal 2", Authors = ["Valve"] });

        Assert.Equal(DuplicateLevel.None, result.Level);
        Assert.Empty(result.Matches);
    }

    // 防：修改時和自己比對（每次存檔都說重複）
    [Fact]
    public async Task CheckAsync_ExcludeId_SkipsItself()
    {
        var id = await CreateWorkAsync(Work("Portal 2", "Valve", SteamUrl));

        var result = await CheckAsync(new CheckDuplicateRequest
        {
            TitleJa = "Portal 2", Authors = ["Valve"], Urls = [SteamUrl], ExcludeId = id
        });

        Assert.Equal(DuplicateLevel.None, result.Level);
    }

    // 防：已刪除的作品也被拿來比對
    [Fact]
    public async Task CheckAsync_DeletedWork_IsIgnored()
    {
        var id = await CreateWorkAsync(Work("Portal 2", "Valve", SteamUrl));
        await using (var db = CreateDbContext())
            await CreateService(db).DeleteAsync(id);

        var result = await CheckAsync(new CheckDuplicateRequest { TitleJa = "Portal 2", Urls = [SteamUrl] });

        Assert.Equal(DuplicateLevel.None, result.Level);
    }

    // 防：沒有名稱也沒有網址時，空字串對上空字串而全部算重複
    [Fact]
    public async Task CheckAsync_NothingToCompare_ReturnsNone()
    {
        await CreateWorkAsync(Work("Portal 2"));

        var result = await CheckAsync(new CheckDuplicateRequest { TitleZh = "  ", Authors = ["Valve"], Urls = [""] });

        Assert.Equal(DuplicateLevel.None, result.Level);
        Assert.Empty(result.Matches);
    }

    private async Task<List<DuplicateGroupDto>> ScanAsync()
    {
        await using var db = CreateDbContext();
        return await new DuplicateService(db).ScanAsync();
    }

    // 防：名稱的桶子 key 前綴前後不一致（找不到桶子而 500）、網址和名稱沒合併成一組、同一部出現兩次
    [Fact]
    public async Task ScanAsync_SameUrlOrTitle_GroupedOnce()
    {
        var a = await CreateWorkAsync(Work("Portal 2", "Valve", SteamUrl));
        var b = await CreateWorkAsync(Work("ポータル２", "Valve", SteamUrl + "/?utm_source=x"));
        await CreateWorkAsync(Work("Portal", "Valve"));   // 續作：不算重複

        var groups = await ScanAsync();

        var group = Assert.Single(groups);
        Assert.Equal(DuplicateLevel.Exact, group.Level);
        Assert.Equal([a, b], group.Items.Select(i => i.Id));
        Assert.Contains("網址相同", group.Reasons);
    }

    // 防：名稱相同沒分到同一組（只看網址）、組裡同時有完全和可能重複時等級取錯、理由少了其中一種
    [Fact]
    public async Task ScanAsync_GroupWithExactAndSimilar_IsExactWithBothReasons()
    {
        var first = await CreateWorkAsync(Work("魔法少女", "Alpha"));
        var sameAuthor = await CreateWorkAsync(Work("[漢化] 魔法少女", "Alpha"));
        var otherAuthor = await CreateWorkAsync(Work("魔法少女", "Beta"));

        var group = Assert.Single(await ScanAsync());

        Assert.Equal(DuplicateLevel.Exact, group.Level);
        Assert.Equal([first, sameAuthor, otherAuthor], group.Items.Select(i => i.Id));
        Assert.Equal(["同作者且作品名相同", "作品名相同"], group.Reasons);
    }

    // 防：完全重複的組沒排在前面；只有可能重複的組被當成完全重複
    [Fact]
    public async Task ScanAsync_ExactGroupsFirst()
    {
        await CreateWorksAsync(Work("魔法少女", "Alpha"), Work("魔法少女", "Beta"));          // 可能重複
        await CreateWorksAsync(Work("Portal 2", url: SteamUrl), Work("別名", url: SteamUrl)); // 完全重複

        var groups = await ScanAsync();

        Assert.Equal([DuplicateLevel.Exact, DuplicateLevel.Similar], groups.Select(g => g.Level));
    }

    // 防：已刪除的作品也被掃進來；沒有重複時回傳了只有一部的組
    [Fact]
    public async Task ScanAsync_DeletedAndSingleWorks_NotListed()
    {
        var deleted = await CreateWorkAsync(Work("Portal 2", "Valve", SteamUrl));
        await CreateWorkAsync(Work("Portal 2", "Valve", SteamUrl));
        await using (var db = CreateDbContext())
            await CreateService(db).DeleteAsync(deleted);

        Assert.Empty(await ScanAsync());
    }
}
