namespace Library.Desktop.Models;

/// <summary>
/// 對應 POST /api/works/check 的 DuplicateCheckDto。Level 是 "none"、"similar"、"exact"（API 的 enum 用名稱傳）。
/// </summary>
public record DuplicateCheckResult(string Level, DuplicateMatch[] Matches)
{
    public bool IsExact => Level == "exact";
}

/// <summary>和既有作品重複的一筆。</summary>
public record DuplicateMatch(int Id, string Title, string[] Authors, string Level, string[] Reasons)
{
    public bool IsExact => Level == "exact";

    /// <summary>提示框顯示的說明，例如「Valve・網址相同」。</summary>
    public string Detail => string.Join("・", Authors.Append(string.Join("、", Reasons)));
}
