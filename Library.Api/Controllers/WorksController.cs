using Library.Api.Data;
using Library.Api.Dtos;
using Library.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/works")]
public class WorksController : ControllerBase
{
    private readonly LibraryDbContext _db;

    public WorksController(LibraryDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkDto>>> GetAll()
    {
        var works = await _db.Works
            .AsNoTracking()
            .ToListAsync();

        return works.Select(WorkDto.From).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkDto>> Get(int id)
    {
        var work = await _db.Works
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (work is null)
            return NotFound();

        return WorkDto.From(work);
    }
    
    [HttpPost]
    public async Task<ActionResult<WorkDto>> Create(SaveWorkRequest request)
    {
        var work = new Work
        {
            TitleZh = request.TitleZh?.Trim() ?? "",
            TitleJa = request.TitleJa?.Trim() ?? "",
            TitleEn = request.TitleEn?.Trim() ?? "",
            Remark = request.Remark?.Trim() ?? "",
            ReleaseDate = request.ReleaseDate,
            Score = request.Score ?? 0,
            Favorite = request.Favorite ?? false,
            Purchased = request.Purchased ?? false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Works.Add(work);
        await _db.SaveChangesAsync();
        
        return CreatedAtAction(nameof(Get), new { id = work.Id }, WorkDto.From(work));
    }
    
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,SaveWorkRequest request)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if(work is null)
            return NotFound();
        
        work.TitleZh = request.TitleZh?.Trim() ?? "";
        work.TitleJa = request.TitleJa?.Trim() ?? "";
        work.TitleEn = request.TitleEn?.Trim() ?? "";
        work.Remark = request.Remark?.Trim() ?? "";
        work.ReleaseDate = request.ReleaseDate ?? null;
        work.Score = request.Score ?? 0;
        work.Favorite = request.Favorite ?? false;
        work.Purchased = request.Purchased ?? false;
        
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return NotFound();
        work.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
    
}