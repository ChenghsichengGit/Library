using library.api.Data;
using library.api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace library.api.Controllers;

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
            .Where(w => !w.DeletedAt.HasValue)
            .ToListAsync();

        return works.Select(WorkDto.From).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkDto>> Get(int id)
    {
        var work = await _db.Works
            .AsNoTracking()
            .Where(w => !w.DeletedAt.HasValue)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (work is null)
            return NotFound();

        return WorkDto.From(work);
    }
}