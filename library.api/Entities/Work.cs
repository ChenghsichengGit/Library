using System.ComponentModel.DataAnnotations;
namespace library.api.Entities;

public class Work
{
    public int Id { get; set; }
    
    public string Title {
        get
        {
            if (!String.IsNullOrEmpty(TitleZh))
                return TitleZh;
            else if (!String.IsNullOrEmpty(TitleJa))
                return TitleJa;
            else if (!String.IsNullOrEmpty(TitleEn))
                return TitleEn;

            return "";
        }
    }
    
    [MaxLength(300)]
    public string TitleZh { get; set; } = "";
    [MaxLength(300)]
    public string TitleJa { get; set; } = "";
    [MaxLength(300)]
    public string TitleEn { get; set; } = "";

    [MaxLength(300)]
    public string Remark { get; set; } = "";
    
    public DateOnly? ReleaseDate { get; set; }
    public int Score { get; set; }
    public bool Favorite { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    
}