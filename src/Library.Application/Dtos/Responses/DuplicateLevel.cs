namespace Library.Application.Dtos.Responses;

/// <summary>重複的程度。數字越大越嚴重，可以直接比大小。</summary>
public enum DuplicateLevel
{
    None,
    Similar,
    Exact
}
