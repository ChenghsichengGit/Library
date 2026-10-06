using System.ComponentModel.DataAnnotations;
using Library.Application.Dtos;

namespace Library.UnitTests;

/// <summary>
/// SaveWorkRequest 驗證規則的單元測試：不需要網站、不需要資料庫。
/// </summary>
/// <remarks>
/// 判斷要用哪個斷言：這筆輸入「應該被接受」→ Assert.Empty(errors)；「應該被拒絕」（Swagger 會回 400）→ Assert.NotEmpty(errors)。
/// 測試方法命名：要測的東西_情境_預期結果。
/// </remarks>
public class SaveWorkRequestTests
{
    // 平常驗證由 ASP.NET Core 自動執行；測試裡沒有它，所以自己呼叫同一套驗證（屬性標記 + IValidatableObject）
    private static List<ValidationResult> Validate(SaveWorkRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }

    [Fact]
    public void Validate_OnlyJapaneseTitle_HasNoErrors()
    {
        var request = new SaveWorkRequest { TitleJa = "テスト作品" };
        var errors = Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NoTitles_HasError()
    {
        var request = new SaveWorkRequest();
        var errors = Validate(request);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_AllTitleSpace_HasError()
    {
        var request = new SaveWorkRequest { TitleZh = "  ", TitleJa = "  ", TitleEn = "  "};
        var errors = Validate(request);
        Assert.NotEmpty(errors);
    }

    // [Theory]：同一個測試用好幾組資料各跑一次；只測邊界（-1、0、6、7），中間的值不會錯
    // 第二個參數 expectValid：這組資料「應該」合法嗎？
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(null, true)]
    public void Validate_Score_MustBeBetween0And6(int? score, bool expectValid)
    {
        var request = new SaveWorkRequest { TitleZh = "TEST", Score = score };

        var errors = Validate(request);

        Assert.Equal(expectValid, errors.Count == 0);
    }
}