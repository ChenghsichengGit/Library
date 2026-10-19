namespace Library.Desktop.Models;

/// <summary>管理視窗清單的一列：作者、社團、系列共用同一個形狀，系列的 Url 是空字串。</summary>
public record ManageEntry(
    int Id,
    string Name,
    int WorkCount,
    string Url,
    string Note,
    string? CoverKey,
    string? CoverUrl)
{
    public static ManageEntry From(CreatorItem c) =>
        new(c.Id, c.Name, c.WorkCount, c.Url, c.Note, c.CoverKey, c.CoverUrl);

    public static ManageEntry From(SeriesItem s) =>
        new(s.Id, s.Name, s.WorkCount, "", s.Note, s.CoverKey, s.CoverUrl);
}
