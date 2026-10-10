namespace Library.Desktop.Models;

/// <summary>
/// 前端的作品資料，對應 API 回傳的 WorkDto。WPF 不引用後端的程式碼，只照 JSON 的格式定義。
/// </summary>
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
    DateTime CreatedAt,
    string[] Authors,
    string[] Circles,
    int WorkTypeId,
    string WorkType)
{
    // 以下只給畫面顯示用

    /// <summary>加入時間的當地時間（API 回傳 UTC）。</summary>
    public DateTime CreatedAtLocal => CreatedAt.ToLocalTime();

    /// <summary>評分的顯示文字：0 不顯示、1～5 星星、6 王冠。</summary>
    public string ScoreText => Score switch
    {
        0 => "",
        6 => "👑",
        _ => new string('★', Score)
    };

    /// <summary>表格用「、」接成一行；表單編輯用換行（MainViewModel.AuthorsInput）。</summary>
    public string AuthorsText => string.Join("、", Authors);

    public string CirclesText => string.Join("、", Circles);
}
