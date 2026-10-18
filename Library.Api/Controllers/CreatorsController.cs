using Library.Application.Dtos;
using Library.Application.Services;
using Library.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
}
