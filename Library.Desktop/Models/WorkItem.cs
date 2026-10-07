namespace Library.Desktop.Models;

/// <summary>
/// 前端的作品資料，對應 API 回傳的 JSON（後端的 WorkDto）。
/// </summary>
/// <remarks>
/// WPF 不引用後端的程式碼，只照著 JSON 的格式自己定義一個類別：雙方只約定 JSON 長什麼樣。
/// JSON 的 title 會自動對應到 Title（大小寫不用管）。
/// 只有寫在這裡的欄位會被讀取：後端加了欄位，這裡也要加，畫面才看得到。
/// </remarks>
public record WorkItem(
    int Id,
    string Title,
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string Remark,
    int Score,
    bool Favorite,
    bool Purchased,
    DateOnly? ReleaseDate,
    DateTime CreatedAt)
{
    // 下面兩個只有 get 的屬性只給畫面顯示用：JSON 轉換時會忽略，也不會送回 API

    /// <summary>加入時間轉成電腦的當地時間（API 回傳的是 UTC，JSON 結尾帶 Z）。</summary>
    public DateTime CreatedAtLocal => CreatedAt.ToLocalTime();

    /// <summary>評分的顯示文字：0 不顯示、1～5 顯示星星、6 顯示王冠。</summary>
    public string ScoreText => Score switch
    {
        // 由上往下比對，_（其他所有情況）一定要放最後，不然 0 和 6 會先被它接走
        0 => "",
        6 => "👑",
        _ => new string('★', Score)
    };
}
