using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Library.Infrastructure.Data;

/// <summary>
/// 讓從資料庫讀出來的 DateTime 標記為 UTC。
/// </summary>
/// <remarks>
/// SQL Server 的 datetime2 不記錄時區，EF Core 讀出來時會標成「未指定」，轉成 JSON 時就不會帶 Z，
/// 前端看不出它是 UTC。程式裡存進去的一律是 UTC，所以讀出來時直接標記為 UTC。
/// </remarks>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    toDb => toDb,                                              // 寫入：照原樣
    fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc))  // 讀出：標記為 UTC（數值不變，只是加上時區標記）
{
}