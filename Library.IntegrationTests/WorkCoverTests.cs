using Library.Application.Dtos;

namespace Library.IntegrationTests;

/// <summary>
/// 作品的封面：key 的儲存、網址的組出，以及換掉封面時刪除不再使用的舊檔。
/// </summary>
[Collection("SqlServer")]
public class WorkCoverTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    private const string OldKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";
    private const string NewKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png";

    private static SaveWorkRequest Request(string? coverKey, string title = "テスト") =>
        new() { TitleJa = title, CoverKey = coverKey, WorkTypeId = 1 };

    // 防：key 沒 Trim、網址沒經過 storage 組出、空的時候回傳 "" 而不是 null、修改時沒送封面卻保留了舊的（PUT 是整筆取代）
    [Fact]
    public async Task CoverKey_SavedTrimmed_AndClearedWhenNotSent()
    {
        var id = await CreateWorkAsync(Request($" {OldKey} "));

        var saved = (await GetWorkAsync(id))!;
        Assert.Equal(OldKey, saved.CoverKey);
        Assert.Equal($"fake/{OldKey}", saved.CoverUrl);

        await UpdateWorkAsync(id, Request(null));

        var cleared = (await GetWorkAsync(id))!;
        Assert.Null(cleared.CoverKey);
        Assert.Null(cleared.CoverUrl);
    }

    [Fact]
    public async Task UpdateAsync_ReplacedCover_DeletesOldFile()
    {
        var id = await CreateWorkAsync(Request(OldKey));

        await UpdateWorkAsync(id, Request(NewKey));

        Assert.Equal([OldKey], Covers.Deleted);
    }

    [Fact]
    public async Task UpdateAsync_ClearedCover_DeletesOldFile()
    {
        var id = await CreateWorkAsync(Request(OldKey));

        await UpdateWorkAsync(id, Request(null));

        Assert.Equal([OldKey], Covers.Deleted);
    }

    // 防：沒比對新舊 key，只改標題也把封面刪掉
    [Fact]
    public async Task UpdateAsync_SameCover_KeepsFile()
    {
        var id = await CreateWorkAsync(Request(OldKey));

        await UpdateWorkAsync(id, Request(OldKey, "改過的標題"));

        Assert.Empty(Covers.Deleted);
    }

    // 防：舊 key 是空字串時還呼叫 DeleteAsync("")
    [Fact]
    public async Task UpdateAsync_FirstCover_DeletesNothing()
    {
        var id = await CreateWorkAsync(Request(null));

        await UpdateWorkAsync(id, Request(NewKey));

        Assert.Empty(Covers.Deleted);
    }

    // 防：沒檢查其他作品，或漏了 IgnoreQueryFilters（軟刪除的作品還原後封面會壞掉）
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAsync_CoverUsedByAnotherWork_KeepsFile(bool otherIsDeleted)
    {
        var id = await CreateWorkAsync(Request(OldKey));
        var otherId = await CreateWorkAsync(Request(OldKey, "別の作品"));
        if (otherIsDeleted)
        {
            await using var db = CreateDbContext();
            await CreateService(db).DeleteAsync(otherId);
        }

        await UpdateWorkAsync(id, Request(NewKey));

        Assert.Empty(Covers.Deleted);
    }

    // 防：只檢查作品，把作者／社團挑來當封面的檔案刪掉
    [Fact]
    public async Task UpdateAsync_CoverUsedByCreator_KeepsFile()
    {
        var request = Request(OldKey);
        request.Authors = ["ZUN"];
        var id = await CreateWorkAsync(request);
        await using (var db = CreateDbContext())
        {
            db.Creators.Single(c => c.Name == "ZUN").CoverKey = OldKey;
            await db.SaveChangesAsync();
        }

        await UpdateWorkAsync(id, Request(NewKey));

        Assert.Empty(Covers.Deleted);
    }

    // 防：只檢查作品，把系列挑來當封面的檔案刪掉
    [Fact]
    public async Task UpdateAsync_CoverUsedBySeries_KeepsFile()
    {
        var request = Request(OldKey);
        request.Series = "東方Project";
        var id = await CreateWorkAsync(request);
        await using (var db = CreateDbContext())
        {
            db.Series.Single(s => s.Name == "東方Project").CoverKey = OldKey;
            await db.SaveChangesAsync();
        }

        await UpdateWorkAsync(id, Request(NewKey));

        Assert.Empty(Covers.Deleted);
    }
}
