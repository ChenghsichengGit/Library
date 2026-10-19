namespace Library.Application.Dtos.Requests;

/// <summary>依多個標籤篩選時的比對方式。</summary>
public enum TagMatch
{
    All, // 每個標籤都要有（AND）
    Any // 有其中一個就好（OR）
}