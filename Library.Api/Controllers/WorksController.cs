using Library.Application.Dtos;
using Library.Application.Exceptions;
using Library.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 作品的 HTTP 入口：呼叫 WorkService，把結果轉成狀態碼。業務規則都在 WorkService。
/// </summary>
/// <remarks>
/// [ApiController] 在進入方法前自動驗證輸入，不通過直接回 400。
/// </remarks>
[ApiController]
[Route("api/works")]
public class WorksController : ControllerBase
{
    private readonly WorkService _works;

    public WorksController(WorkService works)
    {
        _works = works;
    }

    /// <summary>GET /api/works?q=…&amp;workTypeId=…&amp;sort=title&amp;desc=true：作品清單。條件不合法回 400。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkDto>>> GetAll([FromQuery] WorkQuery query)
    {
        return await _works.GetWorksAsync(query);
    }

    /// <summary>GET /api/works/5：單筆作品。找不到回 404。</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkDto>> Get(int id)
    {
        var work = await _works.GetByIdAsync(id);
        return work is null ? NotFound() : work;
    }

    /// <summary>POST /api/works：新增作品。成功回 201 並附上 Location；類型不存在回 400。</summary>
    [HttpPost]
    public async Task<ActionResult<WorkDto>> Create(SaveWorkRequest request)
    {
        try
        {
            var work = await _works.CreateAsync(request);
            return CreatedAtAction(nameof(Get), new { id = work.Id }, work);
        }
        // 只接住這一種：其他例外是 bug，照常變成 500
        catch (WorkTypeNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>PUT /api/works/5：整筆取代作品內容。成功回 204，找不到回 404，類型不存在回 400。</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveWorkRequest request)
    {
        try
        {
            var success = await _works.UpdateAsync(id, request);

            if (success) return NoContent();
            return NotFound();
        }
        catch (WorkTypeNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>DELETE /api/works/5：軟刪除作品。成功回 204，找不到回 404。</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _works.DeleteAsync(id);

        if (success) return NoContent();
        return NotFound();
    }
}
