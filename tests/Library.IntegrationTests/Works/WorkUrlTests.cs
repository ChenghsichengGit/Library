using Library.Application.Dtos.Requests;
using Library.Domain.Entities;
using Library.IntegrationTests.Support;

namespace Library.IntegrationTests.Works;

/// <summary>
/// 作品的商店網址：存檔整理、整批取代、已購買的推導、單獨切換購買狀態。
/// </summary>
[Collection("SqlServer")]
public class WorkUrlTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    // 防：空白列沒濾掉、正規化後相同的網址存兩次、留下後出現的那個、Position 用了 request 的索引、GetByIdAsync 漏了 Include
    [Fact]
    public async Task CreateAsync_SavesUrls_SkipsBlankAndDuplicates()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls =
            [
                new SaveWorkUrlRequest { Url = "  https://store.steampowered.com/app/620/  ", Purchased = true },
                new SaveWorkUrlRequest { Url = "" },
                new SaveWorkUrlRequest { Url = "https://www.dlsite.com/work/RJ123.html", Language = UrlLanguage.Ja },
                new SaveWorkUrlRequest { Url = "store.steampowered.com/app/620" }
            ]
        });

        var result = await GetWorkAsync(id);

        Assert.Equal(2, result!.Urls.Length);
        Assert.Equal("https://store.steampowered.com/app/620/", result.Urls[0].Url);
        Assert.True(result.Urls[0].Purchased);
        Assert.Equal("https://www.dlsite.com/work/RJ123.html", result.Urls[1].Url);
        Assert.Equal(UrlLanguage.Ja, result.Urls[1].Language);
    }

    // 防：UpdateAsync 漏了 Include(Urls)，Clear() 刪不到舊網址，結果 A、B、C 都在
    [Fact]
    public async Task UpdateAsync_ReplacesUrls()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls = [new SaveWorkUrlRequest { Url = "https://a.example.com" }, new SaveWorkUrlRequest { Url = "https://b.example.com" }]
        });

        await UpdateWorkAsync(id, new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls = [new SaveWorkUrlRequest { Url = "https://c.example.com" }]
        });

        Assert.Equal(["https://c.example.com"], (await GetWorkAsync(id))!.Urls.Select(u => u.Url));
    }

    // 防：原價、折扣沒存進資料庫或沒放進 WorkUrlDto（重新讀取後折扣消失）、原價的小數被截掉
    [Fact]
    public async Task CreateAsync_SavesListPriceAndDiscount()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls =
            [
                new SaveWorkUrlRequest
                {
                    Url = "https://store.steampowered.com/app/620", Price = 134.5m, ListPrice = 269.99m, Discount = 50,
                    Currency = "TWD"
                }
            ]
        });

        var url = Assert.Single((await GetWorkAsync(id))!.Urls);
        Assert.Equal(134.5m, url.Price);
        Assert.Equal(269.99m, url.ListPrice);
        Assert.Equal(50, url.Discount);
    }

    // 防：已購買的推導寫成 All 或寫死 false
    [Fact]
    public async Task GetByIdAsync_PurchasedWhenAnyUrlPurchased()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls =
            [
                new SaveWorkUrlRequest { Url = "https://a.example.com" },
                new SaveWorkUrlRequest { Url = "https://b.example.com", Purchased = true }
            ]
        });

        Assert.True((await GetWorkAsync(id))!.Purchased);
    }

    // 防：改到別的網址（Position 比對錯）、沒存檔、沒更新修改時間
    [Fact]
    public async Task SetUrlPurchasedAsync_ChangesOnlyThatUrl()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls = [new SaveWorkUrlRequest { Url = "https://a.example.com" }, new SaveWorkUrlRequest { Url = "https://b.example.com" }]
        });
        Time.Advance(TimeSpan.FromDays(1));

        await using (var db = CreateDbContext())
        {
            Assert.True(await CreateService(db).SetUrlPurchasedAsync(id, 1, true));
        }

        var result = await GetWorkAsync(id);
        Assert.Equal([false, true], result!.Urls.Select(u => u.Purchased));
        Assert.Equal(new DateTime(2026, 1, 2, 12, 0, 0), result.UpdatedAt);
    }

    // 作品不存在、位置超出範圍都是 false（Controller 轉成 404）
    [Fact]
    public async Task SetUrlPurchasedAsync_NotFound_ReturnsFalse()
    {
        var id = await CreateWorkAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            WorkTypeId = 1,
            Urls = [new SaveWorkUrlRequest { Url = "https://a.example.com" }]
        });

        await using var db = CreateDbContext();
        var service = CreateService(db);

        Assert.False(await service.SetUrlPurchasedAsync(999999, 0, true));
        Assert.False(await service.SetUrlPurchasedAsync(id, 1, true));
    }
}
