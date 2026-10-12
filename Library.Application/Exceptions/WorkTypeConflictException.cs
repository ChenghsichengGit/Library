namespace Library.Application.Exceptions;

/// <summary>
/// 類型的操作和現有資料衝突（名稱重複、刪除「其他」）。WorkTypesController 會轉成 409。
/// </summary>
public class WorkTypeConflictException(string message) : Exception(message);
