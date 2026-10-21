namespace Library.Desktop.Models;

/// <summary>對應 GET /api/trash 的回應。</summary>
public record TrashList(int KeepDays, TrashItem[] Items);

/// <summary>最近刪除的一部作品。</summary>
public record TrashItem(int Id, string Title, string WorkType, string[] Authors, string? CoverUrl, DateTime DeletedAt)
{
    /// <summary>例如「Valve · 遊戲 · 3 天前刪除」；沒有作者時顯示「無作者」，同原版。</summary>
    public string Detail => string.Join(" · ", AuthorText, WorkType, DeletedText);

    private string AuthorText => Authors.Length > 0 ? string.Join("、", Authors) : "無作者";

    // 用本地日期計算：晚上 11 點刪除，隔天早上看到的是「1 天前」
    private string DeletedText
    {
        get
        {
            var days = (DateTime.Today - DeletedAt.ToLocalTime().Date).Days;
            return days == 0 ? "今天刪除" : $"{days} 天前刪除";
        }
    }
}
