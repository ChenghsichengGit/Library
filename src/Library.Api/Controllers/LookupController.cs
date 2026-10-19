using Library.Application.Sources;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 用商店網址查詢作品資料：GET /api/lookup?url=…。只回傳資料給前端填表單，不存檔。
/// </summary>
/// <remarks>
/// 400 網址錯誤或不支援、404 商店找不到、502 連不上商店（不是我們的錯，所以不是 500）。錯誤內容是純文字。
/// </remarks>
[ApiController]
[Route("api/lookup")]
public class LookupController(StoreLookupService lookup) : ControllerBase
{
    /// <summary>查詢商店網址對應的作品資料。</summary>
    /// <param name="url">商店的作品頁網址，例如 https://store.steampowered.com/app/620/</param>
    /// <param name="cancellationToken">使用者中斷連線時，一併取消對商店的請求。</param>
    [HttpGet]
    public async Task<ActionResult<StoreWorkInfo>> Get([FromQuery] string url, CancellationToken cancellationToken)
    {
        // RelativeOrAbsolute 會讓 abc 通過，之後讀 Host 會丟例外
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return BadRequest("請輸入完整的網址");

        if (!lookup.CanLookup(uri))
            return BadRequest("不支援這個網站");

        try
        {
            var info = await lookup.LookupAsync(uri, cancellationToken);
            return info is null ? NotFound("找不到這個作品") : Ok(info);
        }
        // 只接住連線失敗和逾時；其他例外是 bug，照常變成 500
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "無法連線到商店，請稍後再試");
        }
    }
}
