using Library.Application.Dtos;
using Library.Application.Exceptions;
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

    /// <summary>POST /api/work-types：新增類型。成功回 201；名稱空白回 400，重複回 409。</summary>
    [HttpPost]
    public async Task<ActionResult<WorkTypeDto>> Create(SaveWorkTypeRequest request)
    {
        try
        {
            var type = await _workTypes.CreateAsync(request);
            // 沒有取得單一類型的端點，Location 指向類型清單
            return CreatedAtAction(nameof(GetAll), type);
        }
        catch (WorkTypeConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>PUT /api/work-types/5：修改名稱與順序。成功回 204；找不到回 404，名稱重複回 409。</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveWorkTypeRequest request)
    {
        try
        {
            var success = await _workTypes.UpdateAsync(id, request);

            if (success) return NoContent();
            return NotFound();
        }
        catch (WorkTypeConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>DELETE /api/work-types/5：刪除類型，使用它的作品改成「其他」。成功回 204；找不到回 404，刪除「其他」回 409。</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var success = await _workTypes.DeleteAsync(id);

            if (success) return NoContent();
            return NotFound();
        }
        catch (WorkTypeConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }
}