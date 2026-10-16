using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>
/// 存檔時送來的一個 DLC。陣列順序就是 DLC 的順序。
/// </summary>
public class SaveWorkDlcRequest
{
    // 名稱必填，但不加 [Required]：沒填名稱的空白列由 WorkService 略過，不讓整個請求失敗
    [MaxLength(500)]
    public string? Name { get; set; }

    [MaxLength(2000)]
    public string? Url { get; set; }

    public bool Purchased { get; set; }

    // 上限對應資料庫的 decimal(12,2)
    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal? Price { get; set; }

    [MaxLength(8)]
    public string? Currency { get; set; }
}
