using System.Text;
using Library.Application.Covers;

namespace Library.UnitTests;

/// <summary>
/// CoverImageFormat 的單元測試。
/// </summary>
public class CoverImageFormatTests
{
    // 每種格式的開頭，後面補一些任意的資料
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D }, ".png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00 }, ".gif")]
    public void DetectExtension_KnownFormat_ReturnsExtension(byte[] header, string expected)
    {
        Assert.Equal(expected, CoverImageFormat.DetectExtension(header));
    }

    // 中間 4 個位元組是檔案大小，每個檔案都不同，不能拿來比對
    [Theory]
    [InlineData(0x00, 0x00, 0x00, 0x00)]
    [InlineData(0x24, 0x3A, 0x01, 0x00)]
    public void DetectExtension_WebP_IgnoresSizeBytes(byte a, byte b, byte c, byte d)
    {
        byte[] header = [.."RIFF"u8, a, b, c, d, .."WEBP"u8];

        Assert.Equal(".webp", CoverImageFormat.DetectExtension(header));
    }

    // 防：只比對 RIFF 開頭（WAV、AVI 也是 RIFF）
    [Fact]
    public void DetectExtension_RiffButNotWebP_ReturnsNull()
    {
        byte[] wav = [.."RIFF"u8, 0x24, 0x00, 0x00, 0x00, .."WAVE"u8];

        Assert.Null(CoverImageFormat.DetectExtension(wav));
    }

    [Fact]
    public void DetectExtension_TextFile_ReturnsNull()
    {
        Assert.Null(CoverImageFormat.DetectExtension(Encoding.UTF8.GetBytes("hello, this is not an image")));
    }

    // 防：資料不到 12 個位元組時讀 header[8..12] 丟出例外
    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0xFF, 0xD8 })]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00 })]
    public void DetectExtension_TooShort_ReturnsNull(byte[] header)
    {
        Assert.Null(CoverImageFormat.DetectExtension(header));
    }
}
