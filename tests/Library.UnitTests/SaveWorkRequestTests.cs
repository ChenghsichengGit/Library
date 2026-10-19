using System.ComponentModel.DataAnnotations;
using Library.Application.Dtos.Requests;

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

    // 只測邊界值
    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_TagLength_Max100(int length, bool expectValid)
    {
        var request = new SaveWorkRequest { TitleZh = "TEST", WorkTypeId = 1, Tags = [new string('a', length)] };

        var errors = Validate(request);

        Assert.Equal(expectValid, errors.Count == 0);
    }

    // 防：少了 ?.，JSON 送來 null 時 Validate 丟出 NullReferenceException
    [Fact]
    public void Validate_NullTag_HasNoErrors()
    {
        var request = new SaveWorkRequest { TitleZh = "TEST", WorkTypeId = 1, Tags = ["A", null!] };
        var errors = Validate(request);

        Assert.Empty(errors);
    }

    // 系列是單一字串，[MaxLength] 直接限制字數
    [Theory]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Validate_SeriesLength_Max300(int length, bool expectValid)
    {
        var request = new SaveWorkRequest { TitleZh = "TEST", WorkTypeId = 1, Series = new string('a', length) };

        var errors = Validate(request);

        Assert.Equal(expectValid, errors.Count == 0);
    }

    // 只接受伺服器產生的 key（Guid "N" ＋ 副檔名），防路徑穿越；null 與 "" 代表沒有封面
    [Theory]
    [InlineData("3f2a9c7de1b04c5a8f6e2d1c0b9a8f7e.jpg", true)]
    [InlineData("3f2a9c7de1b04c5a8f6e2d1c0b9a8f7e.webp", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("../../appsettings.json", false)]
    [InlineData("3f2a9c7de1b04c5a8f6e2d1c0b9a8f7e.exe", false)]
    [InlineData("3F2A9C7DE1B04C5A8F6E2D1C0B9A8F7E.jpg", false)]
    [InlineData("3f2a9c7d-e1b0-4c5a-8f6e-2d1c0b9a8f7e.jpg", false)]
    [InlineData("3f2a9c7de1b04c5a8f6e2d1c0b9a8f7e.jpg/../x", false)]
    public void Validate_CoverKey_OnlyServerGeneratedKey(string? coverKey, bool expectValid)
    {
        var request = new SaveWorkRequest { TitleZh = "TEST", WorkTypeId = 1, CoverKey = coverKey };

        var errors = Validate(request);

        Assert.Equal(expectValid, errors.Count == 0);
    }
}
