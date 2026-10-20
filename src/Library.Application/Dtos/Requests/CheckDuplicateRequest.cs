namespace Library.Application.Dtos.Requests;

/// <summary>重複檢查：表單目前的名稱、作者、網址。修改時傳 ExcludeId 排除自己。</summary>
public class CheckDuplicateRequest
{
    public string? TitleZh { get; set; }
    public string? TitleJa { get; set; }
    public string? TitleEn { get; set; }
    public List<string>? Authors { get; set; }
    public List<string>? Urls { get; set; }
    public int? ExcludeId { get; set; }
}
