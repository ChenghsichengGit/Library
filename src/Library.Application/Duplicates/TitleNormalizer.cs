using System.Text;
using System.Text.RegularExpressions;

namespace Library.Application.Duplicates;

/// <summary>
/// 把同一部作品的不同寫法轉成同一個字串，用來比對標題是否重複。只用於比對，不存檔。
/// </summary>
/// <remarks>
/// 話、卷、部、集不去掉：「Portal」和「Portal 2」、「XX」和「XX 第2部」是不同作品（續作不該被當成重複）。
/// </remarks>
public static partial class TitleNormalizer
{
    // 括號裡有翻譯、版本字樣的整段：[漢化]、(DL版)、【無修正】…
    [GeneratedRegex(
        @"[\[\(【][^\[\]\(\)【】]{0,20}(漢化|汉化|翻譯|翻译|嵌字|重嵌|修正|個人|个人|生肉|熟肉|無修|无修|dl版|digital)[^\[\]\(\)【】]{0,20}[\]\)】]")]
    private static partial Regex TranslationTag();

    // 只有語言、狀態的括號：【中文】、(完結)…
    [GeneratedRegex(@"[\[\(【](繁|简|簡|中文|中譯|中译|日文|日语|日語|英文|完結|完结|連載|连载|全彩|黑白)[\]\)】]")]
    private static partial Regex LanguageTag();

    // 影片的畫質、編碼
    [GeneratedRegex(@"\b(1080p|720p|480p|2160p|4k|hevc|x264|x265|bluray|web-?dl)\b")]
    private static partial Regex VideoTag();

    /// <summary>NFKC、轉小寫、去掉翻譯／語言／畫質標記，只留文字和數字。null 或空白回傳 ""。</summary>
    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "";

        // 全形英數字、全形括號、半形片假名都轉成標準寫法
        var s = title.Normalize(NormalizationForm.FormKC).ToLowerInvariant();

        s = TranslationTag().Replace(s, " ");
        s = LanguageTag().Replace(s, " ");
        s = VideoTag().Replace(s, " ");

        // 標點、空白、括號都拿掉，只留文字（含中日韓文、ー、々）和數字
        return string.Concat(s.Where(char.IsLetterOrDigit));
    }
}
