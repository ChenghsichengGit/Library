using System.ComponentModel.DataAnnotations;
using Library.Domain.Entities;

namespace Library.Application.Dtos.Requests;

/// <summary>
/// 存檔時送來的一個網址。陣列順序就是網址的順序，第一個是主要網址。
/// </summary>
public class SaveWorkUrlRequest
{
    // 不加 [Required]：表單沒填的空白列由 WorkService 略過，不讓整個請求失敗
    [MaxLength(2000)]
    public string? Url { get; set; }

    public bool Purchased { get; set; }

    public UrlLanguage? Language { get; set; }

    // 上限對應資料庫的 decimal(12,2)
    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal? Price { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal? ListPrice { get; set; }

    [Range(0, 100)]
    public int Discount { get; set; }

    [MaxLength(8)]
    public string? Currency { get; set; }
}
