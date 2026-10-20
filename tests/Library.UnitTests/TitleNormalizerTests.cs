using Library.Application.Duplicates;

namespace Library.UnitTests;

/// <summary>
/// TitleNormalizer 的單元測試：同一部作品的不同寫法要得到同一個字串，續作要得到不同的字串。
/// </summary>
public class TitleNormalizerTests
{
    // 防：沒做 NFKC（全形留著）、沒轉小寫、空白和標點沒拿掉
    [Theory]
    [InlineData("Portal 2")]
    [InlineData("ＰＯＲＴＡＬ　２")]
    [InlineData("portal２")]
    [InlineData("Portal-2!")]
    public void Normalize_WidthCaseAndPunctuation_DoNotMatter(string title)
    {
        Assert.Equal("portal2", TitleNormalizer.Normalize(title));
    }

    // 防：只處理全形英數字，半形片假名沒轉成全形（NFKC 才做得到）
    [Fact]
    public void Normalize_HalfWidthKatakana_BecomesFullWidth()
    {
        Assert.Equal(TitleNormalizer.Normalize("ポータル"), TitleNormalizer.Normalize("ﾎﾟｰﾀﾙ"));
    }

    // 防：翻譯、語言、畫質標記沒去掉；全形括號（NFKC 後變半形）沒處理到
    [Theory]
    [InlineData("[漢化] 魔法少女")]
    [InlineData("魔法少女 (DL版)")]
    [InlineData("【中文】魔法少女")]
    [InlineData("魔法少女【無修正】")]
    [InlineData("（完結）魔法少女")]
    [InlineData("魔法少女 1080p BluRay")]
    public void Normalize_TranslationLanguageAndVideoTags_AreRemoved(string title)
    {
        Assert.Equal("魔法少女", TitleNormalizer.Normalize(title));
    }

    // 防：把括號整段都刪掉（[RPG] 不是雜訊）、把一般的副標題當成雜訊
    [Theory]
    [InlineData("[RPG] 魔法少女", "rpg魔法少女")]
    [InlineData("Portal: Remastered", "portalremastered")]
    public void Normalize_OtherWords_AreKept(string title, string expected)
    {
        Assert.Equal(expected, TitleNormalizer.Normalize(title));
    }

    // 防：照原版去掉話、卷、部、集，續作被判成和第一部重複
    [Theory]
    [InlineData("Portal", "Portal 2")]
    [InlineData("魔法少女", "魔法少女 第2部")]
    [InlineData("魔法少女 Vol.1", "魔法少女 Vol.2")]
    [InlineData("魔法少女 1巻", "魔法少女 2巻")]
    public void Normalize_Sequels_AreDifferent(string first, string sequel)
    {
        Assert.NotEqual(TitleNormalizer.Normalize(first), TitleNormalizer.Normalize(sequel));
    }

    // 防：長音「ー」、「々」被當成標點刪掉；假名中間的「・」沒刪掉
    [Theory]
    [InlineData("ポータル・ストーリー", "ポータルストーリー")]
    [InlineData("人々の物語", "人々の物語")]
    public void Normalize_JapaneseMarks_AreKept(string title, string expected)
    {
        Assert.Equal(expected, TitleNormalizer.Normalize(title));
    }

    // 防：回傳 null（呼叫端用 == 比對時兩個 null 會被當成相同）；只有標點的標題沒變成空字串
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("！？…・")]
    public void Normalize_NothingToCompare_ReturnsEmpty(string? title)
    {
        Assert.Equal("", TitleNormalizer.Normalize(title));
    }
}
