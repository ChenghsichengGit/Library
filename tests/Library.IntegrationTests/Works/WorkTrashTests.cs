using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Application.Services;
using Library.Infrastructure.Data;
using Library.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Works;

/// <summary>
/// TrashService：最近刪除的清單、還原、永久刪除。
/// </summary>
[Collection("SqlServer")]
public class WorkTrashTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    private const string CoverKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";

    private static SaveWorkRequest Work(string titleJa) => new() { TitleJa = titleJa, WorkTypeId = 1 };

    private async Task DeleteWorkAsync(int id)
    {
        await using var db = CreateDbContext();
        await CreateService(db).DeleteAsync(id);
    }

    private TrashService CreateTrash(LibraryDbContext db) => new(db, Covers, new CoverCleaner(db, Covers), Time);

    private async Task<TrashDto> GetTrashAsync()
    {
        await using var db = CreateDbContext();
        return await CreateTrash(db).GetAsync();
    }

    // 防：沒關掉全域篩選（清單永遠是空的）、條件寫反（列出沒刪除的）、排序方向錯
    [Fact]
    public async Task GetAsync_OnlyDeleted_NewestFirst()
    {
        var older = await CreateWorkAsync(Work("先刪的"));
        var newer = await CreateWorkAsync(Work("後刪的"));
        await CreateWorkAsync(Work("沒刪的"));

        await DeleteWorkAsync(older);
        Time.Advance(TimeSpan.FromHours(1));
        await DeleteWorkAsync(newer);

        var trash = await GetTrashAsync();

        Assert.Equal(TrashService.KeepDays, trash.KeepDays);
        Assert.Equal([newer, older], trash.Items.Select(i => i.Id));
    }

    // 防：作者關聯被 WorkCreator 的全域篩選擋掉（作者變成空的）、社團也被列成作者、封面網址沒經過 storage
    [Fact]
    public async Task GetAsync_Item_HasAuthorsTypeCoverAndDeletedAt()
    {
        var request = Work("Portal 2");
        request.Authors = ["Valve"];
        request.Circles = ["社團"];
        request.CoverKey = CoverKey;
        var id = await CreateWorkAsync(request);
        await DeleteWorkAsync(id);

        var item = Assert.Single((await GetTrashAsync()).Items);

        Assert.Equal("Portal 2", item.Title);
        Assert.Equal("漫畫", item.WorkType);
        Assert.Equal(["Valve"], item.Authors);
        Assert.Equal($"fake/{CoverKey}", item.CoverUrl);
        Assert.Equal(Time.GetUtcNow().UtcDateTime, item.DeletedAt);
    }

    // 防：沒有封面時回傳 "fake/" 這種壞掉的網址
    [Fact]
    public async Task GetAsync_NoCover_CoverUrlIsNull()
    {
        var id = await CreateWorkAsync(Work("沒有封面"));
        await DeleteWorkAsync(id);

        Assert.Null(Assert.Single((await GetTrashAsync()).Items).CoverUrl);
    }

    // 防：還原時沒清掉 DeletedAt、軟刪除時就把關聯刪了（還原後作者、標籤、網址不見）
    [Fact]
    public async Task RestoreAsync_WorkBackWithRelations()
    {
        var request = Work("Portal 2");
        request.Authors = ["Valve"];
        request.Tags = ["解謎"];
        request.Urls = [new SaveWorkUrlRequest { Url = "https://store.steampowered.com/app/620" }];
        var id = await CreateWorkAsync(request);
        await DeleteWorkAsync(id);

        await using (var db = CreateDbContext())
            Assert.True(await CreateTrash(db).RestoreAsync(id));

        var work = (await GetWorkAsync(id))!;
        Assert.Equal(["Valve"], work.Authors);
        Assert.Equal(["解謎"], work.Tags);
        Assert.Single(work.Urls);
        Assert.Empty((await GetTrashAsync()).Items);
    }

    // 防：沒刪除的作品也能直接永久刪除、關聯沒設 Cascade（DELETE 撞上外鍵）、封面檔留著
    [Fact]
    public async Task PurgeAsync_DeletesWorkRelationsAndCover_OnlyIfInTrash()
    {
        var request = Work("Portal 2");
        request.Authors = ["Valve"];
        request.Tags = ["解謎"];
        request.CoverKey = CoverKey;
        request.Urls = [new SaveWorkUrlRequest { Url = "https://store.steampowered.com/app/620" }];
        request.Dlcs = [new SaveWorkDlcRequest { Name = "DLC" }];
        var id = await CreateWorkAsync(request);
        var active = await CreateWorkAsync(Work("沒刪的"));
        await DeleteWorkAsync(id);

        await using (var db = CreateDbContext())
        {
            Assert.False(await CreateTrash(db).PurgeAsync(active));
            Assert.True(await CreateTrash(db).PurgeAsync(id));
        }

        await using (var db = CreateDbContext())
            Assert.Equal([active], await db.Works.IgnoreQueryFilters().Select(w => w.Id).ToListAsync());
        Assert.Equal([CoverKey], Covers.Deleted);
    }

    // 防：清空時連沒刪除的作品也刪了、還有作品在用的封面也刪了
    [Fact]
    public async Task EmptyAsync_PurgesAllDeleted_KeepsActiveAndSharedCover()
    {
        const string sharedKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png";
        var first = Work("刪了一");
        first.CoverKey = CoverKey;
        var second = Work("刪了二");
        second.CoverKey = sharedKey;
        var keep = Work("沒刪的");
        keep.CoverKey = sharedKey;

        await DeleteWorkAsync(await CreateWorkAsync(first));
        await DeleteWorkAsync(await CreateWorkAsync(second));
        var active = await CreateWorkAsync(keep);

        await using (var db = CreateDbContext())
            Assert.Equal(2, await CreateTrash(db).EmptyAsync());

        Assert.Empty((await GetTrashAsync()).Items);
        Assert.NotNull(await GetWorkAsync(active));
        Assert.Equal([CoverKey], Covers.Deleted);
    }

    // 防：cutoff 往未來算（全部都被清掉）、比較方向寫反（只留下過期的）
    [Fact]
    public async Task PurgeExpiredAsync_OnlyOlderThanKeepDays()
    {
        var expired = await CreateWorkAsync(Work("過期"));
        var recent = await CreateWorkAsync(Work("還在期限內"));

        await DeleteWorkAsync(expired);
        Time.Advance(TimeSpan.FromDays(2));
        await DeleteWorkAsync(recent);
        // expired 刪了 31 天，recent 刪了 29 天
        Time.Advance(TimeSpan.FromDays(TrashService.KeepDays - 1));

        await using (var db = CreateDbContext())
            Assert.Equal(1, await CreateTrash(db).PurgeExpiredAsync());

        Assert.Equal([recent], (await GetTrashAsync()).Items.Select(i => i.Id));
    }
}
