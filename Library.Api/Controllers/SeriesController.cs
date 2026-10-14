using Library.Application.Dtos;
using Library.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 系列的 HTTP 入口。
/// </summary>
[ApiController]
[Route("api/series")]
public class SeriesController : ControllerBase
{
    private readonly SeriesService _service;

    public SeriesController(SeriesService service)
    {
        _service = service;
    }

    /// <summary>GET /api/series：有作品的系列，附作品數與作者，依名稱排序。</summary>
    [HttpGet]
    public async Task<ActionResult<List<SeriesDto>>> GetAll()
    {
        return await _service.GetAllAsync();
    }
}