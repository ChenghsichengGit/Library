using System.ComponentModel.DataAnnotations;

namespace Library.Application.Dtos;

/// <summary>
/// PATCH 切換購買狀態的內容。
/// </summary>
public class SetPurchasedRequest
{
    // bool? 加 [Required]：沒送時擋下來，宣告成 bool 會變成 false 而被存進去
    [Required]
    public bool? Purchased { get; set; }
}
