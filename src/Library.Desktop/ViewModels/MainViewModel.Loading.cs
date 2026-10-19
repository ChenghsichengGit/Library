using System.Net.Http;
using CommunityToolkit.Mvvm.Input;
using Library.Desktop.Models;
using Library.Desktop.ViewModels.Parts;

namespace Library.Desktop.ViewModels;

/// <summary>從 API 讀取作品清單，以及下拉選單、篩選、自動完成用的選項。</summary>
public partial class MainViewModel
{
    /// <summary>
    /// 依目前的搜尋、篩選、排序條件讀取作品清單。視窗開啟、按重新載入／搜尋、篩選條件改變時執行。
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        // 啟動時 API 還沒開的話類型是空的，之後按「重新載入」時補讀
        if (WorkTypes.Count == 0)
            await LoadWorkTypesAsync();

        if (TagFilters.Count == 0)
            await LoadTagsAsync();

        if (SeriesOptions.Count == 0)
            await LoadSeriesAsync();

        if (!Authors.HasOptions)
            await LoadCreatorsAsync();

        StatusMessage = "載入中…";
        // 勾選標籤會觸發這裡重新查詢，順便更新標題的已選數量
        OnPropertyChanged(nameof(TagFilterHeader));
        try
        {
            var query = new WorkListQuery
            (
                Q: SearchText,
                // 沒勾是「不篩選」（null），送 false 會變成「只要非最愛」
                Favorite: OnlyFavorite ? true : null,
                MinScore: SelectedScoreFilter.Min,
                MaxScore: SelectedScoreFilter.Max,
                WorkTypeId: FilterWorkTypeId == 0 ? null : FilterWorkTypeId,
                CreatorId: FilterCreatorId == 0 ? null : FilterCreatorId,
                SeriesId: FilterSeriesId == 0 ? null : FilterSeriesId,
                DlcUnbought: DlcUnbought,
                Tags: TagFilters
                    .Where(t => t.IsSelected)
                    .Select(t => t.Name)
                    .ToList(),
                TagAny: TagMatchAny,
                Sort: SelectedSort.Value,
                Desc: SortDescending
            );
            var works = await _api.GetWorksAsync(query);
            Works.Clear();
            foreach (var work in works)
                Works.Add(work);
            StatusMessage = $"共 {works.Count} 部作品";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>
    /// 從 API 讀取類型清單。類型很少變動，只在清單還是空的時候由 LoadAsync 呼叫，不隨篩選重讀。
    /// </summary>
    private async Task LoadWorkTypesAsync()
    {
        try
        {
            var workType = await _api.GetWorkTypesAsync();
            WorkTypes.Clear();
            foreach (var type in workType)
            {
                WorkTypes.Add(type);
            }

            WorkTypeFilters.Clear();
            WorkTypeFilters.Add(new WorkTypeItem(0, "全部類型", 0));
            foreach (var type in workType)
            {
                WorkTypeFilters.Add(type);
            }

            // 篩選中的類型被刪掉就回到全部；否則通知繫結重讀，Clear() 後下拉選單才會選回原本的項目
            if (FilterWorkTypeId != 0 && WorkTypes.All(t => t.Id != FilterWorkTypeId))
                FilterWorkTypeId = 0;
            else
                OnPropertyChanged(nameof(FilterWorkTypeId));

            // 選項讀進來之後才有預設值可以給；正在編輯作品時保留它原本的類型
            if (EditingId == null)
                WorkTypeId = DefaultWorkTypeId;
            // 值沒變時 setter 不會通知，Clear() 後下拉選單要靠這行才會選回來
            OnPropertyChanged(nameof(WorkTypeId));
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>
    /// 讀取標籤篩選清單，依名稱保留原本的勾選。啟動時及作品儲存、刪除後執行（作品數會變）。
    /// </summary>
    private async Task LoadTagsAsync()
    {
        try
        {
            var selected = TagFilters
                .Where(t => t.IsSelected)
                .Select(t => t.Name)
                .ToHashSet();

            var tags = await _api.GetTagsAsync();

            TagFilters.Clear();
            foreach (var tag in tags)
            {
                TagFilters.Add(new TagFilterOption(tag, selected.Contains(tag.Name), () => LoadCommand.Execute(null)));
            }

            Tags.SetOptions(tags.Select(t => new NameOption(t.Name, t.WorkCount)));
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>從 API 讀取作者、社團清單，給自動完成用。</summary>
    private async Task LoadCreatorsAsync()
    {
        try
        {
            var authors = await _api.GetCreatorsAsync("author");
            var circles = await _api.GetCreatorsAsync("circle");
            Authors.SetOptions(authors.Select(a => new NameOption(a.Name, a.WorkCount)));
            Circles.SetOptions(circles.Select(c => new NameOption(c.Name, c.WorkCount)));

            CreatorFilters.Clear();
            CreatorFilters.Add(new CreatorItem(0, "全部作者／社團", 0, "", "", null, null));
            // 同一個人可能同時是作者和社團，依 Id 去重複
            foreach (var creator in authors.Concat(circles).DistinctBy(c => c.Id).OrderBy(c => c.Name))
                CreatorFilters.Add(creator);

            // 和類型篩選一樣：篩選中的人不見了就回到全部，否則通知下拉選單選回來
            if (FilterCreatorId != 0 && CreatorFilters.All(c => c.Id != FilterCreatorId))
                FilterCreatorId = 0;
            else
                OnPropertyChanged(nameof(FilterCreatorId));
            // 管理視窗改了網址、備註後，資訊列要更新
            OnPropertyChanged(nameof(FilterCreator));
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>從 API 讀取系列清單。</summary>
    private async Task LoadSeriesAsync()
    {
        try
        {
            var series = await _api.GetSeriesAsync();
            SeriesOptions.Clear();
            foreach (var item in series)
            {
                SeriesOptions.Add(item);
            }

            await LoadSeriesFiltersAsync();
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }

    /// <summary>讀取系列篩選的選項；選了作者時只列他參與的。</summary>
    private async Task LoadSeriesFiltersAsync()
    {
        try
        {
            var series = await _api.GetSeriesAsync(FilterCreatorId == 0 ? null : FilterCreatorId);
            SeriesFilters.Clear();
            SeriesFilters.Add(new SeriesItem(0, "全部系列", 0, []));
            foreach (var item in series)
                SeriesFilters.Add(item);

            if (FilterSeriesId != 0 && series.All(s => s.Id != FilterSeriesId))
                FilterSeriesId = 0;
            else
                OnPropertyChanged(nameof(FilterSeriesId));
        }
        catch (HttpRequestException)
        {
            StatusMessage = "連不上 API，請確認 Library.Api 有在執行";
        }
    }
}
