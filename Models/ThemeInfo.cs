namespace ZenithFiler.Models;

public class ThemeInfo
{
    public string Name { get; }
    public string Description { get; }
    public string Author { get; }
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);

    public ThemeInfo(string name, string? description = null, string? author = null)
    {
        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? "説明はありません" : description;
        Author = author ?? string.Empty;
    }
}
