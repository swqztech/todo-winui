using System.Text.Json;
using MojiTodo.Models;

namespace MojiTodo.Services;

public sealed class TodoStore
{
    private readonly List<TodoItem> _items = [];
    private ulong _nextId = 1;

    public IReadOnlyList<TodoItem> Items => _items;

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "todo-winui");

    public static string StorePath => Path.Combine(AppDataDir, "todos.json");

    public void Load()
    {
        _items.Clear();
        _nextId = 1;
        if (!File.Exists(StorePath))
            return;

        var json = File.ReadAllText(StorePath);
        var list = JsonSerializer.Deserialize<List<TodoItem>>(json);
        if (list is null)
            return;

        foreach (var item in list)
        {
            if (item.Id == 0)
                item.Id = _nextId++;
            if (item.Id >= _nextId)
                _nextId = item.Id + 1;
            _items.Add(item);
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StorePath, json);
    }

    public void Add(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _items.Add(new TodoItem { Id = _nextId++, Text = text, Done = false });
        Save();
    }

    public void AddDone(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _items.Add(new TodoItem { Id = _nextId++, Text = text, Done = true });
        Save();
    }

    public void SetDone(int index, bool done)
    {
        if (index < 0 || index >= _items.Count) return;
        if (_items[index].Done == done) return;
        _items[index].Done = done;
        Save();
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        _items.RemoveAt(index);
        Save();
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _items.Count) return;
        if (toIndex < 0 || toIndex >= _items.Count) return;
        if (fromIndex == toIndex) return;
        var item = _items[fromIndex];
        _items.RemoveAt(fromIndex);
        _items.Insert(toIndex, item);
        Save();
    }

    public void UpdateText(int index, string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        if (index < 0 || index >= _items.Count) return;
        _items[index].Text = text;
        Save();
    }

    public void ImportMarkdown(string content)
    {
        foreach (var raw in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
                AddDone(line[6..].Trim());
            else if (line.StartsWith("- [ ]", StringComparison.Ordinal))
                Add(line[6..].Trim());
            else
                Add(line);
        }
    }

    public string ExportMarkdown()
    {
        return string.Join(Environment.NewLine,
            _items.Select(i => i.Done ? $"- [x] {i.Text}" : $"- [ ] {i.Text}"));
    }

    public string CountsLabel()
    {
        var left = _items.Count(i => !i.Done);
        var done = _items.Count(i => i.Done);
        return $"还剩 {left} · 已完成 {done}";
    }

    public List<int> FilteredIndices(FilterKind kind)
    {
        var result = new List<int>();
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (kind == FilterKind.Active && item.Done) continue;
            if (kind == FilterKind.Done && !item.Done) continue;
            result.Add(i);
        }
        return result;
    }

    public void ReplaceFromJsonFile(string path)
    {
        var json = File.ReadAllText(path);
        File.WriteAllText(StorePath, json);
        Load();
    }
}
