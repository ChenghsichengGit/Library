using Library.Application.Dtos;
using Library.Application.Exceptions;
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

    /// <summary>GET /api/series：有作品的系列，附作品數與作者，依名稱排序。?creatorId=7 只列這個人參與的。</summary>
    [HttpGet]
    public async Task<ActionResult<List<SeriesDto>>> GetAll([FromQuery] int? creatorId)
    {
        return await _service.GetAllAsync(creatorId);
    }

    /// <summary>PUT /api/series/3：修改名稱、備註、封面。成功回 204；找不到回 404，名稱重複回 409。</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveSeriesRequest request)
    {
        try
        {
            var success = await _service.UpdateAsync(id, request);

            if (success) return NoContent();
            return NotFound();
        }
        catch (NameConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>GET /api/series/3/cover-choices：可以挑來當封面的作品封面。</summary>
    [HttpGet("{id:int}/cover-choices")]
    public async Task<ActionResult<List<CoverChoiceDto>>> GetCoverChoices(int id)
    {
        return await _service.GetCoverChoicesAsync(id);
    }
}
