using System.ComponentModel.DataAnnotations;
using Library.Application.Dtos;

namespace Library.UnitTests;

/// <summary>
/// SaveWorkRequest 驗證規則的單元測試。
/// </summary>
public class SaveWorkRequestTests
{
    // 執行和 [ApiController] 同一套驗證：屬性標記 + IValidatableObject
    private static List<ValidationResult> Validate(SaveWorkRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }

    [Fact]
    public void Validate_OnlyJapaneseTitle_HasNoErrors()
    {
        var request = new SaveWorkRequest { TitleJa = "テスト作品", WorkTypeId = 1 };
        var errors = Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NoTitles_HasError()
    {
        var request = new SaveWorkRequest { WorkTypeId = 1 };
        var errors = Validate(request);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_AllTitleSpace_HasError()
    {
        var request = new SaveWorkRequest { TitleZh = "  ", TitleJa = "  ", TitleEn = "  ", WorkTypeId = 1 };
        var errors = Validate(request);
        Assert.NotEmpty(errors);
    }

    // 只測邊界值
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(null, true)]
    public void Validate_Score_MustBeBetween0And6(int? score, bool expectValid)
    {
        var request = new SaveWorkRequest { TitleZh = "TEST", Score = score, WorkTypeId = 1 };

        var errors = Validate(request);

        Assert.Equal(expectValid, errors.Count == 0);
    }

    [Fact]
    public void Validate_NoWorkType_HasError()
    {
        var request = new SaveWorkRequest { TitleZh = "TEST" };
        var errors = Validate(request);

        Assert.NotEmpty(errors);
    }
}