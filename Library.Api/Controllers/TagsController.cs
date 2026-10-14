using Library.Application.Dtos;
using Library.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 標籤的 HTTP 入口。
/// </summary>
[ApiController]
[Route("api/tags")]
public class TagsController : ControllerBase
{
    private readonly TagService _tags;

    public TagsController(TagService tags)
    {
        _tags = tags;
    }

    /// <summary>GET /api/tags：有作品在用的標籤與作品數。</summary>
    [HttpGet]
    public async Task<ActionResult<List<TagDto>>> GetAll()
    {
        return await _tags.GetAllAsync();
    }

    /// <summary>DELETE /api/tags/5：刪除標籤並從作品移除。成功回 204，找不到回 404。</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _tags.DeleteAsync(id))
            return NotFound();
        return NoContent();
    }
}