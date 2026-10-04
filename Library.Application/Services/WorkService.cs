using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

public class WorkService
{
    private readonly ILibraryDbContext _db;
    private readonly TimeProvider _time;
    
    public WorkService(ILibraryDbContext db,  TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<List<WorkDto>> GetWorksAsync()
    {
        var works = await _db.Works
            .AsNoTracking()
            .ToListAsync();

        return works.Select(WorkDto.From).ToList();
    }

    public async Task<WorkDto?> GetByIdAsync(int id)
    {
        var work = await _db.Works
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (work is null)
            return null;

        return WorkDto.From(work);
    }

    public async Task<WorkDto> CreateAsync(SaveWorkRequest request)
    {
        var work = new Work();
        Apply(work, request);
        work.CreatedAt = _time.GetUtcNow().UtcDateTime;

        _db.Works.Add(work);
        await _db.SaveChangesAsync();

        return WorkDto.From(work);
    }

    public async Task<bool> UpdateAsync(int id, SaveWorkRequest request)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;
        
        Apply(work, request);
        
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;
        work.DeletedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync();
        return true;
    }

    private static void Apply(Work work, SaveWorkRequest request)
    {
        work.TitleZh = request.TitleZh?.Trim() ?? "";
        work.TitleJa = request.TitleJa?.Trim() ?? "";
        work.TitleEn = request.TitleEn?.Trim() ?? "";
        work.Remark = request.Remark?.Trim() ?? "";
        work.ReleaseDate = request.ReleaseDate ?? null;
        work.Score = request.Score ?? 0;
        work.Favorite = request.Favorite ?? false;
        work.Purchased = request.Purchased ?? false;
    }
}