using Library.Application.Dtos.Responses;
using Library.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 商店價格的 HTTP 入口。
/// </summary>
[ApiController]
[Route("api/prices")]
public class PricesController : ControllerBase
{
    private readonly PriceUpdateService _service;

    public PricesController(PriceUpdateService service)
    {
        _service = service;
    }

    /// <summary>POST /api/prices/refresh：更新所有商店網址的價格，回傳更新了幾個。使用者取消請求時停止查詢。</summary>
    [HttpPost("refresh")]
    public async Task<PriceUpdateResult> Refresh()
    {
        return await _service.RefreshAsync(HttpContext.RequestAborted);
    }
}
