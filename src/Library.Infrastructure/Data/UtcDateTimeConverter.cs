using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Library.Infrastructure.Data;

/// <summary>
/// 把從資料庫讀出的 DateTime 標記為 UTC。datetime2 不記錄時區，不標記的話 JSON 不會帶 Z。
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    toDb => toDb,
    fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc))
{
}
