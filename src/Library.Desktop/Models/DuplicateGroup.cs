namespace Library.Desktop.Models;

/// <summary>對應 GET /api/works/duplicates 的一組重複作品。</summary>
public record DuplicateGroup(string Level, string[] Reasons, DuplicateGroupItem[] Items)
{
    public bool IsExact => Level == "exact";

    /// <summary>組的標題，例如「⛔ 完全重複：網址相同、作品名相同」。</summary>
    public string Header => (IsExact ? "⛔ 完全重複" : "⚠️ 可能重複") + "：" + string.Join("、", Reasons);
}

/// <summary>重複組裡的一部作品。</summary>
public record DuplicateGroupItem(int Id, string Title, string[] Authors)
{
    // 沒有作者時顯示「未填作者」，同原版
    public string AuthorText => Authors.Length > 0 ? string.Join("、", Authors) : "未填作者";
}
