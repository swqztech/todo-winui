namespace MojiTodo.Models;

public sealed class TodoItem
{
    public ulong Id { get; set; }
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}
