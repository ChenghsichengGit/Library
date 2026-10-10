using Library.Application.Dtos;
using Library.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 作品類型的 HTTP 入口。
/// </summary>
[ApiController]
[Route("api/work-types")]
public class WorkTypesController : ControllerBase
{
    private readonly WorkTypeService _workTypes;

    public WorkTypesController(WorkTypeService workTypes)
    {
        _workTypes = workTypes;
    }

    /// <summary>GET /api/work-types：所有類型，依顯示順序排列。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkTypeDto>>> GetAll()
    {
        return await _workTypes.GetAllAsync();
    }
}
