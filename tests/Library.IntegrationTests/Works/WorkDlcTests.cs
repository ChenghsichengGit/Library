using Library.Application.Dtos.Requests;
using Library.IntegrationTests.Support;


namespace Library.IntegrationTests.Works;

/// <summary>
/// 作品的 DLC／追加內容：存檔整理、整批取代、「還有 DLC 沒買」篩選，以及不影響作品的已購買。
/// </summary>
[Collection("SqlServer")]
public class WorkDlcTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    // 防：空白名稱沒濾掉、名稱沒 Trim、沒有網址的 DLC 被當成重複而只剩一個、Position 用了 request 的索引
    [Fact]
    public async Task CreateAsync_SavesDlcs_SkipsBlankNames()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Dlcs =
            [
                new SaveWorkDlcRequest { Name = "  特典音聲  ", Purchased = true },
                new SaveWorkDlcRequest { Name = "   ", Url = "https://a.example.com" },
                new SaveWorkDlcRequest { Name = "設定資料集", Url = "   " },
                new SaveWorkDlcRequest { Name = "追加劇本", Url = "https://b.example.com" }
            ]
        });

        var result = await GetWorkAsync(id);

        Assert.Equal(["特典音聲", "設定資料集", "追加劇本"], result!.Dlcs.Select(d => d.Name));
        Assert.Equal(["", "", "https://b.example.com"], result.Dlcs.Select(d => d.Url));
        Assert.True(result.Dlcs[0].Purchased);
    }

    // 防：UpdateAsync 漏了 Include(Dlcs)，舊的 DLC 刪不掉
    [Fact]
    public async Task UpdateAsync_ReplacesDlcs()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Dlcs = [new SaveWorkDlcRequest { Name = "A" }, new SaveWorkDlcRequest { Name = "B" }]
        });

        await UpdateWorkAsync(id, new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Dlcs = [new SaveWorkDlcRequest { Name = "C" }]
        });

        Assert.Equal(["C"], (await GetWorkAsync(id))!.Dlcs.Select(d => d.Name));
    }

    // 防：Where 的結果沒存回去（篩選失效）、條件寫反；DLC 全買了或沒有 DLC 的都不列出
    [Fact]
    public async Task GetWorksAsync_FiltersByDlcUnbought()
    {
        await CreateWorksAsync(
            new SaveWorkRequest
            {
                TitleZh = "一個沒買",
                WorkTypeId = 1,
                Dlcs = [new SaveWorkDlcRequest { Name = "A", Purchased = true }, new SaveWorkDlcRequest { Name = "B" }]
            },
            new SaveWorkRequest
            {
                TitleZh = "全部買了",
                WorkTypeId = 1,
                Dlcs = [new SaveWorkDlcRequest { Name = "C", Purchased = true }]
            },
            new SaveWorkRequest { TitleZh = "沒有 DLC", WorkTypeId = 1 });

        var result = await GetWorksAsync(new WorkQuery { DlcUnbought = true });

        Assert.Equal(["一個沒買"], result.Select(w => w.Title));
    }

    // 防：把 DLC 算進作品的「已購買」（原版只看網址）
    [Fact]
    public async Task GetByIdAsync_PurchasedIgnoresDlcs()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls = [new SaveWorkUrlRequest { Url = "https://a.example.com" }],
            Dlcs = [new SaveWorkDlcRequest { Name = "特典", Purchased = true }]
        });

        Assert.False((await GetWorkAsync(id))!.Purchased);
    }
}
