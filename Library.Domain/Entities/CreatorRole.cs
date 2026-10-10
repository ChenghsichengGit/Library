namespace Library.Domain.Entities;

/// <summary>
/// 創作者在某部作品裡的角色。
/// </summary>
/// <remarks>
/// 資料庫存的是數字，既有的值不能調整順序，新角色只能往後加。
/// </remarks>
public enum CreatorRole
{
    /// <summary>作者／開發人員。</summary>
    Author = 0,

    /// <summary>社團／發行商。</summary>
    Circle = 1
}
