// @implements InspectAgentSession
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static partial class LunaCoverageValidation
{
    internal static CoverageAnswer Parse(CoverageInput input, JsonElement answer)
    {
        if (answer.Field("reads").ValueKind != JsonValueKind.Array || answer.Field("unresolved").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Luna returned an incomplete file review. Try again.");
        var reads = new List<TraceFile>();
        var command = input.Command.ReplaceLineEndings("\n").Split('\n');
        bool unresolved = answer.Field("unresolved").GetBoolean();
        foreach (var item in answer.Field("reads").EnumerateArray())
        {
            var literal = item.Str("path").Trim();
            var index = item.Number("operationLine") - 1;
            var proof = index >= 0 && index < command.Length ? command[index].Trim() : "";
            var file = TranscriptPath.Resolve(literal, input.Directory, input.Project);
            if (file.Length == 0 || !Grounded(input, literal, proof)) { unresolved = true; continue; }
            var lines = item.Number("lines");
            var count = answer.Field("reads").GetArrayLength() == 1 ? VerifiedCount(input, proof, lines) : 0;
            reads.Add(new(file, "Read", Lines: count) { ModelEvidence = count > 0 ? proof + "\nLine count evidence: " + input.Result.Trim() : proof });
        }
        return new([.. reads], unresolved);
    }

    [GeneratedRegex("""(?:ReadAllLines\([^;\r\n]*\)\s*\.\s*(?:Length|Count)|len\([^;\r\n]*?(?:splitlines|readlines)\(\)\))[)'";\s]*$""", RegexOptions.IgnoreCase)]
    private static partial Regex CountOperation();

    private static int VerifiedCount(CoverageInput input, string proof, long lines)
    {
        if (!CountOperation().IsMatch(proof) || !int.TryParse(input.Result.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int count)) return 0;
        return lines == 0 || lines == count ? count : 0;
    }

    private static bool Grounded(CoverageInput input, string literal, string proof)
    {
        if (!ValidOperand(literal) || proof.Length < 3) return false;
        if (NonReadOperation().IsMatch(proof) && !ReadOperation().IsMatch(proof)) return false;
        if (!CompatibleOperation(input, proof)) return false;
        var path = literal.Replace('\\', '/').TrimEnd('/');
        if (path.Split('/').Contains("..") && !input.Command.Replace('\\', '/').Contains("../", StringComparison.Ordinal)) return false;
        var reported = input.Result.Replace('\\', '/').Split('\n').Select(ReportedPath).Where(p => p.Length > 0).ToArray();
        return HasPathEvidence(input, proof, path, reported);
    }

    private static bool ValidOperand(string path) => path.Length <= 2048 && path.IndexOfAny(['$', '%', '*', '?', '<', '>', '{', '}', '`']) < 0 &&
        !path.StartsWith('~') && !path.StartsWith('@') && !path.Contains("[redacted]", StringComparison.OrdinalIgnoreCase);

    private static bool CompatibleOperation(CoverageInput input, string proof)
    {
        var shell = Path.GetFileNameWithoutExtension(input.Tool).ToLowerInvariant();
        if (shell == "cmd" && proof.StartsWith("type '", StringComparison.OrdinalIgnoreCase)) return false;
        if (shell is "sh" or "bash" or "zsh" && Regex.IsMatch(proof, @"^cat\s+[^'""\s]+\\[^\s]+$")) return false;
        if (Regex.IsMatch(proof, @"^(?:cat|type|gc|get-content)\s+[\w./:'""\\ -]+$", RegexOptions.IgnoreCase))
            return !string.IsNullOrWhiteSpace(input.Result) && !input.Result.Contains("Cannot find path", StringComparison.OrdinalIgnoreCase);
        if (shell is "bash" or "sh" or "zsh" && Regex.IsMatch(proof, @"^cat\s+[\w./-]+\s*>\s*[\w./-]+$"))
            return string.IsNullOrWhiteSpace(input.Result);
        if (shell is "bash" or "sh" or "zsh" or "cmd" or "pwsh" or "powershell") return !OpaqueCommand().IsMatch(proof);
        return !proof.StartsWith('{') || !proof.Contains("\"cmd\"", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^\s*(?!Get-Content\b|Select-String\b)(?:[a-z]+-[a-z]+)\s+[\w./-]+$", RegexOptions.IgnoreCase)]
    private static partial Regex OpaqueCommand();

    private static bool HasPathEvidence(CoverageInput input, string proof, string path, string[] reported)
    {
        var leaf = path.Split('/')[^1];
        if (leaf is "" or "." or "..") return false;
        if (!HasFilename(input.Command, leaf) && !reported.Contains(path, StringComparer.OrdinalIgnoreCase)) return false;
        var bindings = Bindings(input.Command, proof);
        var confirmedDirectory = reported.Any(target => path.StartsWith(target.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
        if (DynamicRoot().IsMatch(bindings) && !confirmedDirectory && !NamedLocalPath(bindings, path)) return false;
        if (DirectoryOperand(path, proof)) return false;
        var evidence = bindings + "\n" + input.Directory;
        foreach (var target in reported)
            if (path.Equals(target, StringComparison.OrdinalIgnoreCase) || path.StartsWith(target.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)) evidence += "\n" + target;
        evidence = evidence.Replace('\\', '/');
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part is not ("." or "..")).All(part => evidence.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"\$env:|\bos\.environ\b|\bgettempdir\(|\bTemporaryDirectory\(|\bGetTempPath\(", RegexOptions.IgnoreCase)]
    private static partial Regex DynamicRoot();

    private static bool HasFilename(string command, string leaf) =>
        Regex.IsMatch(command, """(?:^|[\s/'"\\(,=])""" + Regex.Escape(leaf) + """(?=$|[\s'"),;|])""", RegexOptions.IgnoreCase);

    private static bool NamedLocalPath(string bindings, string path) => Regex.IsMatch(bindings,
        """(?:\b(?:open|Path|ReadAllLines|ReadAllText)\(\s*|\b(?:cat|Get-Content)\s+)['"]""" + Regex.Escape(path) + """['"]\s*\)?(?!\s*/)""", RegexOptions.IgnoreCase);

    private static bool DirectoryOperand(string path, string proof)
    {
        var operand = Regex.Escape(path);
        return Regex.IsMatch(proof, "['\"]" + operand + "['\"]\\s*\\)?\\s*/\\s*\\w+", RegexOptions.IgnoreCase);
    }

    private static string ReportedPath(string line)
    {
        var at = line.IndexOf(": ", StringComparison.Ordinal);
        var path = (at >= 0 ? line[(at + 2)..] : line).Trim();
        return path.StartsWith('/') || path.Length > 2 && char.IsAsciiLetter(path[0]) && path[1..3] == ":/" ? path : "";
    }

    [GeneratedRegex(@"^\s*(?:git\s+(?:diff|status|log)|python(?:3|\.exe)?\s+[^-]|rg\s|grep\s|echo\s|write-output\s)", RegexOptions.IgnoreCase)]
    private static partial Regex NonReadOperation();

    [GeneratedRegex(@"\b(?:cat|sed|awk|get-content|read_text|read_bytes|readalltext|readalllines|parsefile|open)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReadOperation();

    [GeneratedRegex(@"^\s*(?:(?:for(?:each)?\s*\(?|assert)\s*)?\$?(\w+)\s*(?:=|\bin\b)", RegexOptions.IgnoreCase)]
    private static partial Regex Binding();

    private static string Bindings(string command, string proof)
    {
        var evidence = proof;
        var prior = command.ReplaceLineEndings("\n").Split('\n').TakeWhile(line => line.Trim() != proof);
        foreach (var line in prior.Reverse())
        {
            var binding = Binding().Match(line);
            if (binding.Success && Regex.IsMatch(evidence, @"\b" + Regex.Escape(binding.Groups[1].Value) + @"\b", RegexOptions.IgnoreCase))
                evidence += "\n" + line;
        }
        return evidence;
    }
}
