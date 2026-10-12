namespace Library.Desktop.Models;

/// <summary>
/// 作品類型，對應 API 回傳的 WorkTypeDto。用在下拉選單與類型管理視窗。
/// </summary>
public record WorkTypeItem(int Id, string Name, int SortOrder);
