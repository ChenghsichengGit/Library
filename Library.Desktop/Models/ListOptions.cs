namespace Library.Desktop.Models;

/// <summary>排序下拉選單的選項：Label 是畫面上顯示的文字，Value 是送給 API 的 sort 值。</summary>
public record SortOption(string Label, string Value);

/// <summary>評分篩選下拉選單的選項：Min／Max 對應 API 的 minScore／maxScore，null 代表不限。</summary>
public record ScoreFilter(string Label, int? Min, int? Max);