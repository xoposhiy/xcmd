using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class RefinementList
{
    public static async Task SaveAsync(string path, IEnumerable<FeedbackNote> notes)
    {
        var text = new StringBuilder("# Refinement list\n\n");
        foreach (var note in notes)
        {
            text.Append(note.ResolvedIn == null ? "- [ ] " : "- [x] ").AppendLine(note.Text.Replace("\r", "").Replace("\n", "\n  ", StringComparison.Ordinal));
            text.Append("  - Element: ").Append(note.ElementName).Append(" (`").Append(note.ElementId).AppendLine("`)");
            text.Append("  - Release: ").AppendLine(note.Release);
            text.Append("  - Directory: ").AppendLine(note.PanelPath);
            text.Append("  - Added: ").AppendLine(note.CreatedAt.ToString("O"));
            if (note.ResolvedIn != null) text.Append("  - Resolved in ").Append(note.ResolvedIn).Append(": ").AppendLine(note.Resolution);
            text.AppendLine();
        }
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(temporary, text.ToString());
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
