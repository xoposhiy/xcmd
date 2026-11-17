// @implements FavoriteLocations
namespace Xcmd.Core;

public sealed record FavoriteDirectory(string Id, string Name, string Path);

public sealed class FavoriteDirectories
{
    private readonly List<FavoriteDirectory> items = [];
    public IReadOnlyList<FavoriteDirectory> Items => items;

    public FavoriteDirectory Add(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path)) throw new ArgumentException("Choose an absolute directory path.");
        path = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        var existing = items.FirstOrDefault(item => item.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;
        if (items.Count >= 256) throw new ArgumentException("The favorite list is full. Remove a favorite first.");
        var name = System.IO.Path.GetFileName(path);
        var favorite = new FavoriteDirectory(Guid.NewGuid().ToString("N"), name.Length == 0 ? path : name[..Math.Min(100, name.Length)], path);
        items.Add(favorite);
        return favorite;
    }

    public void Rename(string id, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 100 || name.Any(char.IsControl))
            throw new ArgumentException("Enter a name with 1 to 100 characters and no control characters.");
        var index = items.FindIndex(item => item.Id == id);
        if (index < 0) throw new ArgumentException("The favorite no longer exists.");
        items[index] = items[index] with { Name = name };
    }

    public void Remove(string id) => items.RemoveAll(item => item.Id == id);
    public void Restore(IEnumerable<FavoriteDirectory> favorites) { items.Clear(); items.AddRange(favorites); }
}
