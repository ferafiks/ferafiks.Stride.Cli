namespace ferafiks.Stride.Cli.Core;

public class TextTreeItem(string text, ConsoleColor color)
{
    public TextTreeItem() : this("", ConsoleColor.Gray) { }
    public TextTreeItem(string text) : this(text, ConsoleColor.Gray) { }

    private const string Middle = "├── ";
    private const string Last = "└── ";
    private const string Space = "    ";
    private const string Vertical = "│   ";

    public string Text { get; set; } = text;
    public ConsoleColor Color { get; set; } = color;
    public List<TextTreeItem> Children { get; } = [];

    public void Add(TextTreeItem item) =>
        Children?.Add(item);

    public void WriteToConsole() =>
        WriteItemToConsole(this, "", false, true);

    private void WriteItemToConsole(TextTreeItem item, string indent, bool isLast, bool isFirst = false)
    {
        Console.Write(indent);

        if (!isFirst)
        {
            Console.Write(isLast ? Last : Middle);
            indent += isLast ? Space : Vertical;
        }

        Console.ForegroundColor = item.Color;
        Console.WriteLine(item.Text);
        Console.ForegroundColor = ConsoleColor.Gray;

        int childCount = item.Children.Count;
        for (int i = 0; i < childCount; i++)
            WriteItemToConsole(item.Children[i], indent, i == childCount - 1);
    }
}
