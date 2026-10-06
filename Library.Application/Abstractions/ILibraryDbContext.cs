using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Abstractions;

/// <summary>
/// 「我需要一個能存取作品、能存檔的東西」。Application 只認識這個介面，不知道背後是 SQL Server。
/// 實作在 Infrastructure 的 LibraryDbContext；測試時直接把 LibraryDbContext 傳給 WorkService。
/// </summary>
/// <remarks>
/// 和硬體抽象層一樣：這裡定義「要有什麼功能」，Infrastructure 提供「實際的驅動程式」。
/// 選擇暴露 DbSet（而不是一個查詢一個方法的 Repository），是為了保留 LINQ 自由組合條件的能力。
/// </remarks>
public interface ILibraryDbContext
{
    /// <summary>Works 資料表。可以在上面寫 LINQ（Where、OrderBy…），EF Core 會翻譯成 SQL。</summary>
    DbSet<Work> Works { get; }

    /// <summary>把這次的新增、修改真的寫進資料庫。沒呼叫的話，所有修改都只存在記憶體裡。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
