using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Library.Desktop.Services;

/// <summary>
/// 上傳前在 WPF 縮圖：最長邊縮到 900px，省傳輸也省伺服器空間。規則和原版相同。
/// </summary>
/// <remarks>WPF 沒有 WebP 編碼器，所以輸出 JPEG（有透明度的用 PNG）。不處理 EXIF 方向，手機直拍的照片可能會躺著。</remarks>
public static class CoverImageShrinker
{
    public const int MaxEdge = 900;
    private const int SkipBytes = 200 * 1024;

    /// <summary>回傳要上傳的位元組；不需要縮或縮不了時回傳原本的。</summary>
    public static byte[] Shrink(byte[] original)
    {
        // GIF 縮了會失去動畫
        if (original.AsSpan().StartsWith("GIF8"u8))
            return original;

        BitmapFrame frame;
        try
        {
            // OnLoad：馬上把整張圖解碼完，之後 MemoryStream 被回收也沒關係
            frame = BitmapFrame.Create(new MemoryStream(original), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException)
        {
            // WPF 不認得的格式（例如沒裝 WebP 解碼器）交給伺服器判斷
            return original;
        }

        var longest = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (original.Length <= SkipBytes && longest <= MaxEdge)
            return original;

        // 例：2400×3200 → 最長邊 3200 → 900 / 3200 = 0.28125；本來就比 900 小的不放大
        var scale = Math.Min(1.0, (double)MaxEdge / longest);
        BitmapSource resized = scale < 1 ? new TransformedBitmap(frame, new ScaleTransform(scale, scale)) : frame;

        // 有透明度的圖存 PNG：JPEG 沒有透明，透明的地方會變黑
        BitmapEncoder encoder = HasAlpha(frame.Format)
            ? new PngBitmapEncoder()
            : new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(resized));

        using var output = new MemoryStream();
        encoder.Save(output);

        // 縮完反而變大就用原圖
        return output.Length < original.Length ? output.ToArray() : original;
    }

    /// <summary>剪貼簿的圖片只有像素、沒有檔案，先存成 PNG 的位元組再交給 Shrink。</summary>
    public static byte[] ToPng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static bool HasAlpha(PixelFormat format) =>
        format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32 ||
        format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64;
}
