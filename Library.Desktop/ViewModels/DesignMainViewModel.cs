using Library.Desktop.Services;

namespace Library.Desktop.ViewModels;

/// <summary>
/// 只給 XAML 預覽用的假資料：清單是空的時候 DataTemplate 不會畫出任何卡片。執行時不會建立它。
/// </summary>
/// <remarks>
/// MainWindow.xaml 以 d:DataContext + IsDesignTimeCreatable=True 指定；建立 WorksApiClient 不會連線。
/// </remarks>
public class DesignMainViewModel : MainViewModel
{
    public DesignMainViewModel() : base(new WorksApiClient(), new MessageBoxDialogService())
    {
        TitleJa = "サンプル作品";
        Urls.Add(new UrlRow { Url = "https://store.steampowered.com/app/620", Language = "en", Purchased = true });
        Dlcs.Add(new DlcRow { Name = "特典音聲", PriceText = "330", Currency = "JPY" });
        Dlcs.Add(new DlcRow { Name = "設定資料集", Url = "https://…" });
        Authors.SetNames(["Valve"]);
        Tags.SetNames(["RPG", "2D", "像素風"]);
    }
}
