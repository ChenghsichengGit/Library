namespace Library.Domain.Entities;

/// <summary>
/// 創作者在某部作品裡的角色。記在 WorkCreator（關係）上，不記在 Creator（人）上：
/// 同一個人在不同作品、甚至同一部作品裡，可以擔任不同角色。
/// </summary>
/// <remarks>
/// 資料庫存的是數字，已經存進去的值不能改，調整順序或插入新值會讓舊資料的角色對調，新角色只能往後加。
/// </remarks>
public enum CreatorRole
{
    /// <summary>作者／開發人員。</summary>
    Author = 0,

    /// <summary>社團／發行商。</summary>
    Circle = 1
}
