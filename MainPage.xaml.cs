using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MojiTodo.Helpers;
using MojiTodo.Models;
using MojiTodo.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace MojiTodo;

public sealed partial class MainPage : Page
{
    private readonly TodoStore _store = new();
    private FilterKind _filter = FilterKind.All;
    private readonly ObservableCollection<TodoRow> _rows = [];
    private bool _suppressCheck;

    public MainPage()
    {
        InitializeComponent();
        var now = DateTime.Now;
        var weekdays = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        TxtDate.Text = $"{now.Month}月{now.Day}日{weekdays[(int)now.DayOfWeek]}";
        TaskList.ItemsSource = _rows;
        TaskList.DoubleTapped += OnEditTask;
        _store.Load();
        RefreshList();
        UpdateFilterButtons();
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        _store.Add(TxtInput.Text);
        TxtInput.Text = "";
        RefreshList();
    }

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            OnAdd(sender, e);
            e.Handled = true;
        }
    }

    private void OnFilterAll(object sender, RoutedEventArgs e) => SetFilter(FilterKind.All);
    private void OnFilterActive(object sender, RoutedEventArgs e) => SetFilter(FilterKind.Active);
    private void OnFilterDone(object sender, RoutedEventArgs e) => SetFilter(FilterKind.Done);

    private void SetFilter(FilterKind kind)
    {
        _filter = kind;
        UpdateFilterButtons();
        RefreshList();
    }

    private void UpdateFilterButtons()
    {
        StyleChip(BtnAll, _filter == FilterKind.All);
        StyleChip(BtnActive, _filter == FilterKind.Active);
        StyleChip(BtnDone, _filter == FilterKind.Done);
    }

    private void StyleChip(Button btn, bool active)
    {
        btn.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        btn.Background = active
            ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 40, 48, 58))
            : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 250, 253));
        btn.Foreground = active
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : (Brush)Resources["TitleBrush"]!;
    }

    private void RefreshList()
    {
        _suppressCheck = true;
        _rows.Clear();
        foreach (var idx in _store.FilteredIndices(_filter))
        {
            var item = _store.Items[idx];
            _rows.Add(new TodoRow { StoreIndex = idx, Text = item.Text, Done = item.Done });
        }
        _suppressCheck = false;
        TxtStats.Text = _store.CountsLabel();
    }

    private TodoRow? SelectedRow() => TaskList.SelectedItem as TodoRow;

    private void OnTaskCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressCheck) return;
        if (sender is not CheckBox cb || cb.DataContext is not TodoRow row) return;
        _store.SetDone(row.StoreIndex, cb.IsChecked == true);
        RefreshList();
    }

    private async void OnEditTask(object sender, DoubleTappedRoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is null) return;
        var input = new TextBox { Text = row.Text, Width = 320 };
        var dlg = new ContentDialog
        {
            Title = "编辑待办",
            Content = input,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(input.Text)) return;
        _store.UpdateText(row.StoreIndex, input.Text);
        RefreshList();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is null)
        {
            await ShowInfo("请先选中一条待办");
            return;
        }
        var dlg = new ContentDialog
        {
            Title = "删除",
            Content = "删除这条待办？",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        _store.RemoveAt(row.StoreIndex);
        RefreshList();
    }

    private void OnMoveUp(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void OnMoveDown(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        var row = SelectedRow();
        if (row is null) return;
        var newIdx = row.StoreIndex + delta;
        if (newIdx < 0 || newIdx >= _store.Items.Count) return;
        _store.MoveItem(row.StoreIndex, newIdx);
        RefreshList();
        foreach (var r in _rows)
        {
            if (r.StoreIndex == newIdx)
            {
                TaskList.SelectedItem = r;
                break;
            }
        }
    }

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Markdown", [".md"]);
        picker.SuggestedFileName = "墨迹清单-export";
        PickerHelper.InitPicker(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await Windows.Storage.FileIO.WriteTextAsync(file, _store.ExportMarkdown());
        await ShowInfo("已导出");
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".json");
        PickerHelper.InitPicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        var ext = Path.GetExtension(file.Path).ToLowerInvariant();
        if (ext == ".json")
            _store.ReplaceFromJsonFile(file.Path);
        else
            _store.ImportMarkdown(await File.ReadAllTextAsync(file.Path));
        RefreshList();
        await ShowInfo("导入完成");
    }

    private async void OnPaste(object sender, RoutedEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            await ShowInfo("剪贴板为空");
            return;
        }
        var text = await content.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            await ShowInfo("剪贴板为空");
            return;
        }
        _store.ImportMarkdown(text);
        RefreshList();
        await ShowInfo("已从剪贴板导入");
    }

    private async Task ShowInfo(string message)
    {
        var dlg = new ContentDialog
        {
            Title = "墨迹清单",
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        };
        await dlg.ShowAsync();
    }
}

public sealed class TodoRow
{
    public int StoreIndex { get; set; }
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}
