namespace Library.Domain.Entities;

/// <summary>
/// 網址所屬的語言版本，對應作品的三種名稱。資料庫存名稱（不是數字），API 以 zh／ja／en 表示。
/// </summary>
public enum UrlLanguage
{
    Zh,
    Ja,
    En
}
