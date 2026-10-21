using Library.Application.Dtos.Responses;
using Library.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 最近刪除的 HTTP 入口。
/// </summary>
[ApiController]
[Route("api/trash")]
public class TrashController : ControllerBase
{
    private readonly TrashService _service;

    public TrashController(TrashService service)
    {
        _service = service;
    }

    /// <summary>GET /api/trash：已刪除的作品，最近刪除的在前。</summary>
    [HttpGet]
    public async Task<TrashDto> GetAll()
    {
        return await _service.GetAsync();
    }

    /// <summary>POST /api/trash/8/restore：還原。成功回 204；不在最近刪除裡回 404。</summary>
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        return await _service.RestoreAsync(id) ? NoContent() : NotFound();
    }

    /// <summary>DELETE /api/trash/8：永久刪除。成功回 204；不在最近刪除裡回 404。</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Purge(int id)
    {
        return await _service.PurgeAsync(id) ? NoContent() : NotFound();
    }

    /// <summary>POST /api/trash/empty：清空，回傳刪了幾部。</summary>
    [HttpPost("empty")]
    public async Task<object> EmptyTrash()
    {
        return new { removed = await _service.EmptyAsync() };
    }
}
