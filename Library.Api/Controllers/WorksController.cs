using Library.Application.Dtos;
using Library.Application.Services;
using Library.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/works")]
public class WorksController : ControllerBase
{
    private readonly WorkService _works;

    public WorksController(WorkService works)
    {
        _works = works;
    }
    
    [HttpGet]
    public async Task<ActionResult<List<WorkDto>>> GetAll()
    {
        return await _works.GetWorksAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkDto>> Get(int id)
    {
        var work = await _works.GetByIdAsync(id);
        return work is null ? NotFound() : work;
    }
    
    [HttpPost]
    public async Task<ActionResult<WorkDto>> Create(SaveWorkRequest request)
    {
        var work = await _works.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = work.Id }, work);
    }
    
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,SaveWorkRequest request)
    {   
        var success = await _works.UpdateAsync(id, request);
        
        if(success)  return NoContent();
        return NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _works.DeleteAsync(id);

        if(success)  return NoContent();
        return NotFound();
    }
    
}