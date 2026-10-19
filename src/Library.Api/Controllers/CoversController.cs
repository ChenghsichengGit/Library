using Library.Application.Abstractions;
using Library.Application.Exceptions;
using Library.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 上傳或從網址下載封面，回傳 key（給 WPF 填進作品的 CoverKey）和網址（預覽用）。
/// </summary>
/// <remarks>400 不是支援的圖片或太大、502 下載不到。只存檔，要等儲存作品時才和作品綁在一起。</remarks>
[ApiController]
[Route("api/[controller]")]
public class CoversController(CoverService covers, ICoverStorage storage) : ControllerBase
{
    public record UploadResult(string Key, string Url);

    /// <summary>上傳圖片檔：POST /api/covers（multipart，欄位名稱 file）。</summary>
    [HttpPost]
    [RequestSizeLimit(CoverService.MaxBytes + 1024 * 1024)] // 多留 1MB 給 multipart 的表頭
    public async Task<ActionResult<UploadResult>> Upload(IFormFile file)
    {
        // Application 不依賴 ASP.NET 的 IFormFile，所以在這裡轉成 byte[]
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        try
        {
            var key = await covers.SaveAsync(stream.ToArray());
            return new UploadResult(key, storage.GetUrl(key));
        }
        catch (InvalidCoverException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public record DownloadRequest(string Url);

    /// <summary>從網址下載圖片：POST /api/covers/download，例如抓取商店資料時拿到的封面網址。</summary>
    [HttpPost("download")]
    public async Task<ActionResult<UploadResult>> Download(DownloadRequest request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            return BadRequest("請輸入完整的網址");

        try
        {
            var key = await covers.DownloadAsync(uri, cancellationToken);
            return new UploadResult(key, storage.GetUrl(key));
        }
        catch (InvalidCoverException ex)
        {
            return BadRequest(ex.Message);
        }
        // 對方網站的問題，不是我們的錯 → 502
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "無法下載這張圖片");
        }
    }
}
