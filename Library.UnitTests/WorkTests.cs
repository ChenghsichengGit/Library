using Library.Domain.Entities;

namespace Library.UnitTests;

public class WorkTests
{
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