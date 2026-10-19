using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Application.Exceptions;
using Library.Application.Services;
using Library.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 作者、社團的 HTTP 入口。
/// </summary>
[ApiController]
[Route("api/creators")]
public class CreatorsController(CreatorService creators) : ControllerBase
{
    /// <summary>GET /api/creators?role=author：作者（或 circle 社團）與作品數。沒給 role 回 400。</summary>
    [HttpGet]
    public async Task<ActionResult<List<CreatorDto>>> GetAll([FromQuery, BindRequired] CreatorRole role)
    {
        return await creators.GetAllAsync(role);
    }

    /// <summary>PUT /api/creators/7：修改名稱、網址、備註、封面。成功回 204；找不到回 404，名稱重複回 409。</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveCreatorRequest request)
    {
        try
        {
            var success = await creators.UpdateAsync(id, request);

            if (success) return NoContent();
            return NotFound();
        }
        catch (NameConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>GET /api/creators/7/cover-choices：可以挑來當封面的作品封面。</summary>
    [HttpGet("{id:int}/cover-choices")]
    public async Task<ActionResult<List<CoverChoiceDto>>> GetCoverChoices(int id)
    {
        return await creators.GetCoverChoicesAsync(id);
    }
}
