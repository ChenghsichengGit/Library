using Library.Domain.Entities;

namespace Library.UnitTests;

/// <summary>
/// Work 的單元測試。
/// </summary>
public class WorkTests
{
    // 每個 [InlineData] 是一組資料，依序對應方法的參數 zh、ja、en、expected
    // Assert.Equal 的第一個參數是「預期值」，第二個是「實際值」，順序反了失敗訊息會講反
    [Theory]
    [InlineData("中文", "日本語", "English", "中文")]
    [InlineData("", "日本語", "English", "日本語")]
    [InlineData("", "", "English", "English")]
    [InlineData("", "", "", "")]
    public void Title_PicksFirstNonEmpty_InZhJaEnOrder(string zh, string ja, string en, string expected)
    {
        var work = new Work {TitleZh = zh, TitleJa = ja, TitleEn = en};
        var title = work.Title;
        Assert.Equal(expected, title);
    }
}