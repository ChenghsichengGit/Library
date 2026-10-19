namespace Library.Desktop.Models;

/// <summary>
/// 新增與修改類型時送給 API 的內容，對應後端的 SaveWorkTypeRequest。
/// </summary>
public record SaveWorkTypeRequest(string Name, int? SortOrder);
