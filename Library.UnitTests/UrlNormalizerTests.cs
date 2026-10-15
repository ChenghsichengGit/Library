using Library.Application.Urls;

namespace Library.UnitTests;

/// <summary>
/// UrlNormalizer 的單元測試。
/// </summary>
public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://store.steampowered.com/app/620/", "store.steampowered.com/app/620")]
    [InlineData("HTTP://WWW.DLsite.com/maniax/work/=/product_id/RJ123456.html",
        "dlsite.com/maniax/work/=/product_id/RJ123456.html")]
    [InlineData("dlsite.com/home", "dlsite.com/home")]
    [InlineData("  https://example.com/  ", "example.com")]
    [InlineData("https://example.com/a///", "example.com/a")]
    public void Normalize_SamePageWrittenDifferently_ReturnsCanonicalForm(string url, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(url));
    }

    [Theory]
    [InlineData("https://example.com/p?utm_source=x&id=5", "example.com/p?id=5")]
    [InlineData("https://example.com/p?FBCLID=abc", "example.com/p")]
    [InlineData("https://example.com/p?_ga=1&gclid=2&ref=3&a=1", "example.com/p?a=1")]
    public void Normalize_TrackingParameters_AreRemoved(string url, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(url));
    }

    [Fact]
    public void Normalize_Parameters_AreSortedByName()
    {
        Assert.Equal("example.com/p?a=1&b=2", UrlNormalizer.Normalize("https://example.com/p?b=2&a=1"));
    }

    // 只轉小寫或只去 www 的實作，在這裡會分不出差別：兩種寫法要得到同一個結果
    [Fact]
    public void Normalize_HostCaseAndWww_DoNotMatter()
    {
        Assert.Equal(
            UrlNormalizer.Normalize("https://www.DLsite.com/home"),
            UrlNormalizer.Normalize("https://dlsite.COM/home"));
    }

    [Fact]
    public void Normalize_PathCase_IsKept()
    {
        Assert.NotEqual(
            UrlNormalizer.Normalize("https://dlsite.com/work/RJ123456.html"),
            UrlNormalizer.Normalize("https://dlsite.com/work/rj123456.html"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_Empty_ReturnsNull(string? url)
    {
        Assert.Null(UrlNormalizer.Normalize(url));
    }
}
