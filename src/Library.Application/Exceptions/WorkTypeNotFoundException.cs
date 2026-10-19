namespace Library.Application.Exceptions;

/// <summary>
/// 新增或修改作品時指定的類型不存在。WorksController 會轉成 400。
/// </summary>
public class WorkTypeNotFoundException : Exception
{
    public int WorkTypeId { get; }

    public WorkTypeNotFoundException(int workTypeId)
        : base($"找不到類型 {workTypeId}")
    {
        WorkTypeId = workTypeId;
    }
}
