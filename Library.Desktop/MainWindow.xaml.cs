using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Library.Desktop.Services;
using Library.Desktop.ViewModels;
using Wpf.Ui.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Controls = System.Windows.Controls;

namespace Library.Desktop;

/// <summary>
/// 主視窗的 code-behind：建立 ViewModel 並接上畫面；拖放、貼上這類畫面事件在這裡轉成位元組交給 ViewModel，不放邏輯。
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // 在這裡決定實作：正式執行用真的 API 和 MessageBox
        _viewModel = new MainViewModel(new WorksApiClient(), new MessageBoxDialogService());
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    // 拖曳經過時決定游標：是圖片檔才顯示「複製」，否則顯示禁止
    private void CoverArea_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = FindImageFile(e.Data) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void CoverArea_Drop(object sender, DragEventArgs e)
    {
        if (FindImageFile(e.Data) is { } path)
            await _viewModel.UploadCoverCommand.ExecuteAsync(await File.ReadAllBytesAsync(path));
    }

    // 剪貼簿是圖片時才攔下 Ctrl+V；有文字就不攔，讓文字框照常貼上
    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        var image = ReadClipboardImage();
        if (image is null)
            return;

        // 要在 await 之前設定：await 之後事件已經傳下去，文字框會收到 Ctrl+V
        e.Handled = true;
        await _viewModel.UploadCoverCommand.ExecuteAsync(image);
    }

    // 依序找：檔案總管複製的檔案 → 瀏覽器放的 PNG → 一般的點陣圖
    private static byte[]? ReadClipboardImage()
    {
        try
        {
            if (Clipboard.ContainsText())
                return null;

            var data = Clipboard.GetDataObject();
            if (data is null)
                return null;

            if (FindImageFile(data) is { } path)
                return File.ReadAllBytes(path);

            // 瀏覽器複製圖片時會放一份 PNG；GetImage() 的點陣圖在透明處常會變黑
            if (data.GetData("PNG") is MemoryStream png)
                return png.ToArray();

            return Clipboard.GetImage() is { } bitmap ? CoverImageShrinker.ToPng(bitmap) : null;
        }
        catch (COMException)
        {
            // 剪貼簿正被其他程式佔用
            return null;
        }
    }

    // 空點：點到的不是任何可以操作的東西 → 取消選取，回到新增模式（新增模式中不清空，見 MainViewModel.Deselect）
    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject clicked)
            return;

        if (HasAncestor(clicked, IsInteractive))
            return;

        _viewModel.DeselectCommand.Execute(null);
    }

    // 點了會有反應的東西，以及整個編輯表單（點偏了不能把打到一半的內容清掉）
    private bool IsInteractive(DependencyObject element) =>
        element == EditForm
        || element is ButtonBase // 按鈕、勾選框、下拉選單的開關、Expander 的標題
            or TextBoxBase or Controls.PasswordBox
            or Controls.ComboBox or Controls.DatePicker or Controls.Calendar
            or Controls.ListBoxItem // 表格的資料列、下拉選單的選項
            or Controls.GridViewColumnHeader // 表格的欄位標題（拖曳調整欄寬）
            or ScrollBar or Thumb or Controls.Slider
        || element is TitleBar; // 拖曳標題列移動視窗

    // 從點到的元素一路往上找，有任何一層符合 match 就回傳 true（像 Unity 的 GetComponentInParent）
    private static bool HasAncestor(DependencyObject? element, Func<DependencyObject, bool> match)
    {
        while (element is not null)
        {
            if (match(element))
                return true;

            // 文字裡的 Run 不是 Visual，沒有視覺上的父元素，要改用邏輯樹往上找
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    private static string? FindImageFile(IDataObject data) =>
        (data.GetData(DataFormats.FileDrop) as string[])?.FirstOrDefault(path =>
            Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp");
}
