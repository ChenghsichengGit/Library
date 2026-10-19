using Library.Application.Dtos.Requests;
using Library.Application.Exceptions;
using Library.Application.Urls;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>新增與修改共用的「把 request 套用到作品」。</summary>
public partial class WorkService
{
    /// <summary>
    /// 新增與修改共用：把 request 填進 work，並把作者、社團、標籤、網址整份換成 request 的內容、設定系列（名字已存在就沿用）。
    /// 只改記憶體，由呼叫端存檔；不設定建立時間。
    /// </summary>
    private async Task ApplyAsync(Work work, SaveWorkRequest request)
    {
        work.TitleZh = request.TitleZh?.Trim() ?? "";
        work.TitleJa = request.TitleJa?.Trim() ?? "";
        work.TitleEn = request.TitleEn?.Trim() ?? "";
        work.Description = request.Description?.Trim() ?? "";
        work.ReleaseDate = request.ReleaseDate;
        work.Score = request.Score ?? 0;
        work.Favorite = request.Favorite ?? false;
        work.CoverKey = request.CoverKey?.Trim() ?? "";

        // [Required] 已擋掉 null。先查類型再處理作者，不存在就不必多查一次資料庫
        var typeId = request.WorkTypeId!.Value;
        var type = await _db.WorkTypes.FindAsync(typeId);
        if (type is null)
            throw new WorkTypeNotFoundException(typeId);
        // 設定物件而不是 Id：CreateAsync 最後的 WorkDto.From 要讀類型名稱
        work.WorkType = type;

        var authors = CleanNames(request.Authors);
        var circles = CleanNames(request.Circles);

        // 名字一次查完；Dictionary 不分大小寫，和 SQL Server 的比對規則一致，否則會重複建立而撞上唯一索引
        var allNames = authors.Concat(circles).ToList();
        var creators = await _db.Creators
            .Where(c => allNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase);

        work.Creators.Clear();
        foreach (var name in authors)
            work.Creators.Add(new WorkCreator { Creator = GetOrCreate(name), Role = CreatorRole.Author });
        foreach (var name in circles)
            work.Creators.Add(new WorkCreator { Creator = GetOrCreate(name), Role = CreatorRole.Circle });

        // 標籤只有一份名單且已去重複，新建的不必放回 Dictionary；WorkTags 由 EF 寫入
        var tagNames = CleanNames(request.Tags);
        var tags = await _db.Tags
            .Where(t => tagNames.Contains(t.Name))
            .ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        work.Tags.Clear();
        foreach (var name in tagNames)
        {
            if (!tags.TryGetValue(name, out var tag))
                tag = new Tag { Name = name };
            work.Tags.Add(tag);
        }

        // 單一名稱直接在資料庫比對（不分大小寫），不需要 Dictionary
        var seriesName = request.Series?.Trim();
        if (string.IsNullOrEmpty(seriesName))
        {
            work.Series = null;
        }
        else
        {
            work.Series = await _db.Series.FirstOrDefaultAsync(s => s.Name == seriesName)
                          ?? new Series { Name = seriesName };
        }

        // 空白列略過；正規化後相同的只留第一個。Position 用整理後的索引
        var urls = (request.Urls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u.Url))
            .DistinctBy(u => UrlNormalizer.Normalize(u.Url))
            .ToList();

        // 外鍵必填，移出清單的網址由 EF 刪除（孤兒刪除）
        work.Urls.Clear();
        for (var i = 0; i < urls.Count; i++)
        {
            work.Urls.Add(new WorkUrl
            {
                Position = i,
                Url = urls[i].Url!.Trim(),
                Purchased = urls[i].Purchased,
                Language = urls[i].Language,
                Price = urls[i].Price,
                Currency = urls[i].Currency?.Trim().ToUpperInvariant() ?? ""
            });
        }

        // DLC 以名稱為準，空白名稱的列略過；不去除重複：沒有網址的 DLC 正規化後都是 null，會被當成同一個
        var dlcs = (request.Dlcs ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .ToList();

        work.Dlcs.Clear();
        for (var i = 0; i < dlcs.Count; i++)
        {
            work.Dlcs.Add(new WorkDlc
            {
                Position = i,
                Name = dlcs[i].Name!.Trim(),
                Url = dlcs[i].Url?.Trim() ?? "",
                Purchased = dlcs[i].Purchased,
                Price = dlcs[i].Price,
                Currency = dlcs[i].Currency?.Trim().ToUpperInvariant() ?? ""
            });
        }

        // 新建的放回 Dictionary：作者和社團是同一個新名字時，才不會建立兩次
        Creator GetOrCreate(string name)
        {
            if (!creators.TryGetValue(name, out var creator))
            {
                creator = new Creator { Name = name };
                creators[name] = creator;
            }

            return creator;
        }
    }

    /// <summary>
    /// 整理名字清單：濾掉 null 和空白、去前後空白、不分大小寫去重複（保留先出現的寫法）。
    /// </summary>
    private static List<string> CleanNames(List<string>? names) =>
        (names ?? [])
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => n.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
