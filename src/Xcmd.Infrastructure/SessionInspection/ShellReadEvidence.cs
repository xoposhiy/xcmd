// @implements InspectAgentSession
using System.Text.RegularExpressions;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static partial class ShellReadEvidence
{
    [GeneratedRegex("'[^'\\r\\n]*'|\"[^\"$`\\\\\\r\\n]*\"")]
    private static partial Regex ShellQuotes();

    // Match the entire command. A read inside quoted text, a branch or a script has no separate result.
    [GeneratedRegex("""\G[ \t]*(?:(?<quoted>'[^'\r\n]*'|"[^"$`\r\n]*")|(?<word>[\p{L}\p{N}_./\\:+@=~-]+)|(?<symbol>[,|]))""")]
    private static partial Regex Token();

    [GeneratedRegex(@"^(?<first>[1-9]\d*)(?:,(?<last>[1-9]\d*))?p$")]
    private static partial Regex SedRange();

    internal static TraceFile[] Read(string shell, string command, string output, string cwd, string project)
    {
        if (string.IsNullOrEmpty(output) || HasDiagnostic(output)) return [];
        var name = Path.GetFileNameWithoutExtension(shell).ToLowerInvariant();
        if (name is "bash" or "sh" or "zsh" && command.Contains("&&", StringComparison.Ordinal))
            return SuccessfulChain(command, cwd, project);
        var words = Tokens(command);
        if (words.Count < 2) return [];
        var read = new LiteralRead();
        if (!read.Parse(name, words, command)) return [];
        return read.Files(cwd, project, output);
    }

    private static TraceFile[] SuccessfulChain(string command, string cwd, string project)
    {
        // A zero exit status proves each command of a literal AND-list succeeded.
        // Stop at a command that could change directory or terminate the shell.
        var structure = ShellQuotes().Replace(command, match => new string('x', match.Length));
        if (structure.Contains("||", StringComparison.Ordinal) || structure.Replace("&&", "").Any(c =>
            !(char.IsAsciiLetterOrDigit(c) || " \t.,/_-|".Contains(c)))) return [];
        var files = new List<TraceFile>();
        var offset = 0;
        foreach (var segment in structure.Split("&&"))
        {
            var words = Tokens(command.Substring(offset, segment.Length));
            offset += segment.Length + 2;
            if (words.Count == 0 || words.Contains("|")) break;
            if (words[0] == "echo" || words is ["cat"]) continue;
            var read = new LiteralRead();
            if (!read.Parse("sh", words, "")) break;
            files.AddRange(read.Paths(cwd, project));
        }
        return files.Distinct().ToArray();
    }

    private static bool HasDiagnostic(string output) => output.Contains('\0') ||
        output.Contains("Get-Content:", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("Cannot find path", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("Access to the path", StringComparison.OrdinalIgnoreCase);

    private static List<string> Tokens(string command)
    {
        var words = new List<string>();
        command = command.Trim();
        for (int at = 0; at < command.Length;)
        {
            var m = Token().Match(command, at);
            if (!m.Success || m.Index != at) return [];
            var value = m.Groups["quoted"].Success ? m.Groups["quoted"].Value : "";
            if (value.Length > 0)
            {
                if (value.IndexOfAny(['`', '$', '*', '?', '[', ']', '%', '!', '|', ',']) >= 0) return [];
                value = value[1..^1];
                if (value.Length == 0 || value.StartsWith('-')) return [];
            }
            else value = m.Groups["word"].Success ? m.Groups["word"].Value : m.Groups["symbol"].Value;
            at += m.Length;
            if (at < command.Length && !char.IsWhiteSpace(command[at]) && command[at] is not (',' or '|') && value is not ("," or "|")) return [];
            words.Add(value);
        }
        return words;
    }

    private sealed class LiteralRead
    {
        private readonly List<string> paths = [];
        private int skip, limit = int.MaxValue;
        private bool whole = true, raw, tail, bytes;

        internal bool Parse(string shell, List<string> words, string command)
        {
            if (words.Count < 2) return false;
            if (shell is "pwsh" or "powershell")
                return words[0].ToLowerInvariant() is ("cat" or "type" or "gc" or "get-content") && PowerShell(words);
            if (shell is "bash" or "sh" or "zsh") return Posix(words);
            if (shell != "cmd" || words.Count != 2 || !words[0].Equals("type", StringComparison.OrdinalIgnoreCase) || command.Contains('\'')) return false;
            paths.Add(words[1]);
            return true;
        }

        private bool PowerShell(List<string> words)
        {
            bool nextPath = true;
            for (int i = 1; i < words.Count; i++)
            {
                switch (words[i].ToLowerInvariant())
                {
                    case "-literalpath": case "-path": break;
                    case "-raw": raw = true; break;
                    case "-encoding":
                        if (++i >= words.Count || words[i].StartsWith('-')) return false;
                        bytes = words[i].Equals("Byte", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "-tail":
                    case "-totalcount":
                    case "-head":
                    case "-first":
                        if (!whole) return false;
                        tail = words[i].Equals("-tail", StringComparison.OrdinalIgnoreCase);
                        if (!ReadLimit(words, ++i)) return false;
                        whole = false;
                        break;
                    case ",":
                        if (nextPath) return false;
                        nextPath = true;
                        break;
                    case "|":
                        if (++i >= words.Count || !words[i].Equals("Select-Object", StringComparison.OrdinalIgnoreCase)) return false;
                        if (!whole) return false;
                        whole = false;
                        return !nextPath && SelectRange(words, i + 1, out skip, out limit);
                    default:
                        if (!nextPath || words[i].StartsWith('-')) return false;
                        paths.Add(words[i]); nextPath = false; break;
                }
            }
            return !nextPath;
        }

        private bool ReadLimit(List<string> words, int index) =>
            index < words.Count && int.TryParse(words[index], out limit) && limit > 0;

        private static bool SelectRange(List<string> words, int start, out int skip, out int limit)
        {
            skip = 0; limit = int.MaxValue;
            if (start == words.Count) return false;
            for (var i = start; i < words.Count; i += 2)
            {
                if (i + 1 >= words.Count || !int.TryParse(words[i + 1], out var count) || count < 0) return false;
                switch (words[i].ToLowerInvariant())
                {
                    case "-skip" when count < int.MaxValue: skip = count; break;
                    case "-first" when count > 0: limit = count; break;
                    default: return false;
                }
            }
            return true;
        }

        private bool Posix(List<string> words)
        {
            if (words.Any(w => w.Contains('\\'))) return false;
            if (words[0] == "cat") paths.AddRange(words.Skip(words[1] == "--" ? 2 : 1));
            else if (words[0] == "sed" && words.Count is 4 or 6 && words[1] == "-n" &&
                SedRange().Match(string.Concat(words.Skip(2).SkipLast(1))) is { Success: true } range &&
                int.TryParse(range.Groups["first"].Value, out var first))
            {
                var last = first;
                if (range.Groups["last"].Success && (!int.TryParse(range.Groups["last"].Value, out last) || last < first)) return false;
                paths.Add(words[^1]); skip = first - 1; limit = last - first + 1; whole = false;
            }
            else return false;
            return true;
        }

        internal TraceFile[] Paths(string cwd, string project)
        {
            if (paths.Count == 0 || paths.Any(p => p.StartsWith('-') || p.StartsWith('~') || p.StartsWith('@') || p is "," or "|")) return [];
            var files = paths.Select(p => new TraceFile(TranscriptPath.Resolve(p, cwd, project), "Read")).ToArray();
            return files.Any(f => f.Path.Length == 0) ? [] : files;
        }

        internal TraceFile[] Files(string cwd, string project, string output)
        {
            var files = Paths(cwd, project);
            if (files.Length == 0) return [];
            if (files.Length > 1 || tail || bytes || output.Contains("truncated", StringComparison.OrdinalIgnoreCase)) return files.Distinct().ToArray();
            if (raw)
            {
                // PowerShell writes a record terminator after the raw string. Keep counts only when it is identifiable.
                if (!whole || !output.EndsWith("\r\n", StringComparison.Ordinal)) return files;
                output = output[..^2];
            }
            var lines = output.Count(ch => ch == '\n') + (output.EndsWith('\n') || output.Length == 0 ? 0 : 1);
            if (lines == 0 || lines > limit) return files;
            return [files[0] with { FirstLine = skip + 1, Lines = lines, TotalLines = whole ? lines : 0 }];
        }
    }
}
