using Library.Application.Abstractions;
using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Application.Duplicates;
using Library.Application.Urls;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 重複偵測：網址相同、或同作者且名稱相同是完全重複；名稱相同但作者不同是可能重複。
/// 名稱用 TitleNormalizer、網址用 UrlNormalizer 正規化後比對。
/// </summary>
public class DuplicateService(ILibraryDbContext db)
{
    // 桶子 key 的前綴：網址和名稱分開，避免網址剛好和某個名稱相同
    private const string UrlKey = "url:";
    private const string TitleKey = "title:";

    /// <summary>新增、修改時檢查表單的內容和哪些既有作品重複，最多回傳 20 筆，完全重複的在前。</summary>
    public async Task<DuplicateCheckDto> CheckAsync(CheckDuplicateRequest request)
    {
        var titles = NormalizeTitles(request.TitleZh, request.TitleJa, request.TitleEn);
        var urls = (request.Urls ?? []).Select(UrlNormalizer.Normalize).OfType<string>().ToHashSet();
        var authors = (request.Authors ?? []).Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 沒有名稱也沒有網址：沒東西可比
        if (titles.Count == 0 && urls.Count == 0)
            return new DuplicateCheckDto(DuplicateLevel.None, []);

        var works = await LoadWorksAsync(request.ExcludeId);

        var matches = new List<DuplicateMatchDto>();
        foreach (var w in works)
        {
            var sameUrl = w.Urls.Overlaps(urls);
            var sameTitle = w.Titles.Overlaps(titles);
            var sameAuthor = w.AuthorSet.Overlaps(authors);

            var reasons = new List<string>();
            if (sameUrl) reasons.Add("網址相同");
            if (sameTitle) reasons.Add(sameAuthor ? "同作者且作品名相同" : "作品名相同");
            if (reasons.Count == 0)
                continue;

            var level = sameUrl || (sameTitle && sameAuthor) ? DuplicateLevel.Exact : DuplicateLevel.Similar;
            matches.Add(new DuplicateMatchDto(w.Id, w.Title, w.Authors.Order().ToArray(), level, reasons.ToArray()));
        }

        var top = matches
            .OrderByDescending(match => match.Level)
            .Take(20)
            .ToArray();
        // 一筆都沒有時 Max 會丟例外，所以先判斷
        var overall = top.Length == 0 ? DuplicateLevel.None : top.Max(m => m.Level);
        return new DuplicateCheckDto(overall, top);
    }

    /// <summary>全庫掃描：網址或名稱相同的作品分成一組（2 部以上才列出），完全重複的組在前。</summary>
    public async Task<List<DuplicateGroupDto>> ScanAsync()
    {
        var works = await LoadWorksAsync(excludeId: null);

        // 同一個 key 的作品放進同一個桶子
        var buckets = new Dictionary<string, List<WorkKeys>>();
        foreach (var w in works)
        {
            foreach (var url in w.Urls)
                AddTo(buckets, UrlKey + url, w);
            foreach (var title in w.Titles)
                AddTo(buckets, TitleKey + title, w);
        }

        var grouped = new HashSet<int>();
        var groups = new List<DuplicateGroupDto>();
        foreach (var first in works)
        {
            if (grouped.Contains(first.Id))
                continue;

            // 和 first 在任何一個桶子裡相遇的其他作品
            var others = first.Urls.Select(u => UrlKey + u)
                .Concat(first.Titles.Select(t => TitleKey + t))
                .SelectMany(key => buckets[key])
                .Where(w => w.Id != first.Id && !grouped.Contains(w.Id))
                .DistinctBy(w => w.Id)
                .ToList();
            if (others.Count == 0)
                continue;

            // 每一部都和這組的第一部比，理由和等級的規則同 CheckAsync
            var level = DuplicateLevel.Similar;
            var reasons = new List<string>();
            foreach (var w in others)
            {
                var sameUrl = w.Urls.Overlaps(first.Urls);
                var sameTitle = w.Titles.Overlaps(first.Titles);
                var sameAuthor = w.AuthorSet.Overlaps(first.AuthorSet);

                if (sameUrl) reasons.Add("網址相同");
                if (sameTitle) reasons.Add(sameAuthor ? "同作者且作品名相同" : "作品名相同");
                if (sameUrl || (sameTitle && sameAuthor))
                    level = DuplicateLevel.Exact;
            }

            List<WorkKeys> members = [first, .. others];
            foreach (var m in members)
                grouped.Add(m.Id);

            groups.Add(new DuplicateGroupDto(level, reasons.Distinct().ToArray(),
                members.Select(m => new DuplicateGroupItemDto(m.Id, m.Title, m.Authors.Order().ToArray())).ToArray()));
        }

        return groups.OrderByDescending(g => g.Level).ToList();
    }

    private static void AddTo(Dictionary<string, List<WorkKeys>> buckets, string key, WorkKeys work)
    {
        if (!buckets.TryGetValue(key, out var list))
            buckets[key] = list = [];
        list.Add(work);
    }

    // 三個名稱正規化後去重複，空的不放
    private static HashSet<string> NormalizeTitles(params string?[] titles) =>
        titles.Select(TitleNormalizer.Normalize).Where(t => t != "").ToHashSet();

    // 比對要用的資料：先在 SQL 只取需要的欄位，正規化（C# 方法，翻不成 SQL）在記憶體做
    private async Task<List<WorkKeys>> LoadWorksAsync(int? excludeId)
    {
        var rows = await db.Works
            .Where(w => w.Id != excludeId)
            .Select(w => new
            {
                w.Id, w.Title, w.TitleZh, w.TitleJa, w.TitleEn,
                Authors = w.Creators.Where(wc => wc.Role == CreatorRole.Author).Select(wc => wc.Creator.Name).ToList(),
                Urls = w.Urls.Select(u => u.Url).ToList()
            })
            .ToListAsync();

        return rows.Select(r => new WorkKeys(
            r.Id, r.Title, r.Authors,
            NormalizeTitles(r.TitleZh, r.TitleJa, r.TitleEn),
            r.Urls.Select(UrlNormalizer.Normalize).OfType<string>().ToHashSet(),
            r.Authors.ToHashSet(StringComparer.OrdinalIgnoreCase))).ToList();
    }

    // 一部作品的比對用資料，CheckAsync 和 ScanAsync 共用
    private record WorkKeys(
        int Id,
        string Title,
        List<string> Authors,
        HashSet<string> Titles,
        HashSet<string> Urls,
        HashSet<string> AuthorSet);
}
