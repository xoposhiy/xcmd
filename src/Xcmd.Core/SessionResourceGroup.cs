// @implements InspectAgentSession
namespace Xcmd.Core;

public sealed class SessionResourceGroup(string key, string name, bool directorySegment = false)
{
    private readonly SortedDictionary<string, SessionResourceGroup> children = new(StringComparer.Ordinal);
    private readonly List<TouchedResource> resources = [];
    private (bool IsDirectory, long Start)? firstPathAccess;
    public string Key { get; } = key;
    public string Name { get; } = name;
    public TouchedResource? Directory { get; private set; }
    public bool HasChildren => children.Count > 0 || resources.Count > 0;
    public IEnumerable<SessionResourceGroup> Children => children.Values.OrderBy(group => group.firstPathAccess?.Start);
    public IEnumerable<TouchedResource> Resources => resources
        .OrderBy(row => row.Resource.Kind is "file" or "directory" ? (long?)FirstAccess(row) : null)
        .ThenBy(row => row.Resource.Value, StringComparer.Ordinal);

    public (string Label, SessionResourceGroup Group) CompactPath()
    {
        var group = this;
        var label = Name;
        while (directorySegment && group.Directory == null && group.resources.Count == 0 && group.children.Count == 1)
        {
            group = group.children.Values.First();
            label += "/" + group.Name;
        }
        return (label, group);
    }

    public static SessionResourceGroup Build(SessionTrace trace, string project, IEnumerable<TouchedResource> visible)
    {
        var root = new SessionResourceGroup("", "");
        foreach (var row in visible)
        {
            var group = root;
            if (row.Resource.Kind is "file" or "directory")
            {
                var path = row.Resource.Value.Replace('\\', '/');
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var keys = trace.FileKey(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
                var directory = row.Resource.Kind == "directory";
                var folderCount = directory ? parts.Length : Math.Max(0, parts.Length - 1);
                var (location, skip) = Location(path, parts, project, folderCount);
                var access = (directory, FirstAccess(row));
                group = root.Group("path:" + trace.FileKey(location), location);
                group.IncludePathAccess(access);
                for (var i = skip; i < folderCount; i++)
                {
                    if (parts[i] != ".")
                    {
                        group = group.Group(keys[i], parts[i], directorySegment: true);
                        group.IncludePathAccess(access);
                    }
                }
                if (directory && group.Directory == null)
                {
                    group.Directory = row;
                    continue;
                }
            }
            else
            {
                group = root.Group("kind:" + row.Resource.Kind, KindName(row.Resource.Kind));
                if (row.Resource.Kind == "url" && Uri.TryCreate(row.Resource.Value, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
                    group = group.Group(uri.Authority, uri.Authority);
            }
            group.resources.Add(row);
        }
        return root;
    }

    private static long FirstAccess(TouchedResource row) => row.Calls.Min(call => (long?)call.Start) ?? long.MaxValue;

    private void IncludePathAccess((bool IsDirectory, long Start) access)
    {
        // Use directory calls only when the group has no matching file calls.
        if (firstPathAccess is not { } first || access.CompareTo(first) < 0)
            firstPathAccess = access;
    }

    private SessionResourceGroup Group(string id, string title, bool directorySegment = false)
    {
        if (!children.TryGetValue(id, out var child))
            children.Add(id, child = new(Key + "/" + id, title, directorySegment));
        return child;
    }

    private static (string Name, int Skip) Location(string path, string[] parts, string project, int folderCount)
    {
        if (path.StartsWith("//", StringComparison.Ordinal))
            return ("//" + string.Join('/', parts.Take(2)), 2);
        if (path.StartsWith('/')) return ("/", 0);
        if (parts.Length > 0 && parts[0].EndsWith(':')) return (parts[0] + "/", 1);
        var parents = parts.TakeWhile(part => part == "..").Count();
        if (parents == 0) return (project, 0);
        var length = Math.Min(parents + 1, folderCount);
        return (string.Join('/', parts.Take(length)), length);
    }

    public static string KindName(string kind) => kind switch
    {
        "file" => "Files",
        "directory" => "Directories",
        "url" => "URLs",
        "issue" => "Issues",
        "repository" => "Repositories",
        _ => "Other"
    };
}
