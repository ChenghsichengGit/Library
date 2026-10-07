using Library.Application.Dtos;
using Library.Application.Services;
using Library.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

/// <summary>
/// 作品的 HTTP 入口：收到請求 → 呼叫 WorkService → 把結果轉成 HTTP 狀態碼。
/// 這裡不放業務規則，規則都在 WorkService。
/// </summary>
/// <remarks>
/// [ApiController]：進到方法之前自動驗證輸入，不通過直接回 400，方法不會被呼叫。
/// [Route("api/works")]：這個類別負責 /api/works 開頭的網址。
/// </remarks>
[ApiController]
[Route("api/works")]
public class WorksController : ControllerBase
{
    private readonly WorkService _works;

    // 不用自己 new：每個請求進來時，DI 容器會建立 Controller 並把 WorkService 傳進來
    public WorksController(WorkService works)
    {
        _works = works;
    }

    /// <summary>GET /api/works?q=…&amp;minScore=…&amp;sort=title&amp;desc=true：作品清單，可加篩選與排序。回 200，條件不合法回 400。</summary>
    // [FromQuery]：從網址的 ?q=… 讀取；不寫的話會以為要從 request body 讀，但 GET 沒有 body
    [HttpGet]
    public async Task<ActionResult<List<WorkDto>>> GetAll([FromQuery] WorkQuery query)
    {
        return await _works.GetWorksAsync(query);
    }

    /// <summary>GET /api/works/5：單筆作品。找不到回 404。</summary>
    // {id:int}：這段網址必須是整數，/api/works/abc 會直接得到 404
    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkDto>> Get(int id)
    {
        var work = await _works.GetByIdAsync(id);
        return work is null ? NotFound() : work;
    }

    /// <summary>POST /api/works：新增作品。成功回 201，並在 Location 標頭附上新作品的網址。</summary>
    [HttpPost]
    public async Task<ActionResult<WorkDto>> Create(SaveWorkRequest request)
    {
        var work = await _works.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = work.Id }, work);
    }

    /// <summary>PUT /api/works/5：整筆取代作品內容。成功回 204（沒有內容），找不到回 404。</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,SaveWorkRequest request)
    {
        var success = await _works.UpdateAsync(id, request);

        if(success)  return NoContent();
        return NotFound();
    }

    /// <summary>DELETE /api/works/5：軟刪除作品。成功回 204，找不到回 404。</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _works.DeleteAsync(id);

        if(success)  return NoContent();
        return NotFound();
    }

}
