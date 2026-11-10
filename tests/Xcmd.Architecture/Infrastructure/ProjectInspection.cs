using System.Diagnostics;
using System.Text.Json;

namespace Xcmd.Architecture.Infrastructure;

internal static class ProjectInspection
{
    private static readonly Dictionary<string, string[]> References = new(StringComparer.Ordinal)
    {
        ["Xcmd.Core"] = [],
        ["Xcmd.Infrastructure"] = ["Xcmd.Core"],
        ["Xcmd.Desktop"] = ["Xcmd.Core", "Xcmd.Infrastructure"],
        ["Xcmd.Probe"] = ["Xcmd.Desktop", "Xcmd.LaunchProbe"],
        ["Xcmd.LaunchProbe"] = [],
        ["Xcmd.Architecture"] = ["Xcmd.Core", "Xcmd.Infrastructure", "Xcmd.Desktop", "Xcmd.Probe"]
    };

    private static readonly Dictionary<string, string[]> Packages = new(StringComparer.Ordinal)
    {
        ["Xcmd.Core"] = ["Microsoft.CodeAnalysis.BannedApiAnalyzers"],
        ["Xcmd.Infrastructure"] = ["Microsoft.CodeAnalysis.BannedApiAnalyzers"],
        ["Xcmd.Desktop"] = ["Microsoft.CodeAnalysis.BannedApiAnalyzers", "Avalonia.Desktop", "Avalonia.Themes.Fluent"],
        ["Xcmd.Probe"] = ["Avalonia.Headless", "Avalonia.Skia"],
        ["Xcmd.LaunchProbe"] = [],
        ["Xcmd.Architecture"] = ["TngTech.ArchUnitNET"]
    };

    public static async Task CheckAsync(string root)
    {
        var projects = new[] { "src", "tests" }.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.csproj", SearchOption.AllDirectories)).ToArray();
        if (projects.Length != References.Count) throw new InvalidOperationException("Every project must have an explicit architecture policy.");
        foreach (var project in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            if (!References.ContainsKey(name)) throw new InvalidOperationException("Missing architecture policy: " + project);
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                using var manifest = await ReadManifestAsync(project, configuration);
                var properties = manifest.RootElement.GetProperty("Properties");
                if (properties.GetProperty("AssemblyName").GetString() != name) throw new InvalidOperationException("Unexpected assembly name: " + project);
                var items = manifest.RootElement.GetProperty("Items");
                RequireSame(project, "direct assembly references", items.GetProperty("Reference").EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!), []);
                RequireSame(project, "framework references", items.GetProperty("FrameworkReference").EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!), ["Microsoft.NETCore.App"]);
                var references = items.GetProperty("ProjectReference").EnumerateArray().Select(item => Path.GetFileNameWithoutExtension(item.GetProperty("FullPath").GetString()!));
                RequireSame(project, "project references", references!, References[name]);
                var packages = items.GetProperty("PackageReference").EnumerateArray().ToArray();
                RequireSame(project, "package references", packages.Select(item => item.GetProperty("Identity").GetString()!), Packages[name]);
                if (packages.Any(item => !Version.TryParse(item.GetProperty("Version").GetString(), out _))) throw new InvalidOperationException("Package versions must be pinned: " + project);
            }
        }
        await CheckLauncherAsync(Path.Combine(root, "src", "Xcmd.Infrastructure", "Xcmd.Infrastructure.csproj"));
        CheckPolicyRejectsForbiddenReferences();
        Console.WriteLine($"PASS evaluated project and package references ({projects.Length} projects, Debug and Release)");
    }

    private static async Task CheckLauncherAsync(string project)
    {
        using var manifest = await ReadManifestAsync(project, "Release", "-p:XcmdLauncher=true");
        if (manifest.RootElement.GetProperty("Properties").GetProperty("AssemblyName").GetString() != "Xcmd")
            throw new InvalidOperationException("The launcher must produce Xcmd.exe.");
        var items = manifest.RootElement.GetProperty("Items");
        RequireSame(project, "launcher project references", items.GetProperty("ProjectReference").EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!), []);
        RequireSame(project, "launcher packages", items.GetProperty("PackageReference").EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!),
            ["Microsoft.CodeAnalysis.BannedApiAnalyzers"]);
        Console.WriteLine("PASS launcher build has no project or UI dependencies");
    }

    private static async Task<JsonDocument> ReadManifestAsync(string project, string configuration, params string[] options)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "msbuild", project, "-nologo", "-p:Configuration=" + configuration, "-getItem:ProjectReference,PackageReference,Reference,FrameworkReference", "-getProperty:AssemblyName" }) start.ArgumentList.Add(argument);
        foreach (var option in options) start.ArgumentList.Add(option);
        using var process = Process.Start(start) ?? throw new IOException("MSBuild could not be started.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        if (process.ExitCode != 0) throw new InvalidOperationException(await error);
        return JsonDocument.Parse(await output);
    }

    private static void RequireSame(string project, string kind, IEnumerable<string> actual, IEnumerable<string> allowed)
    {
        if (!actual.Order(StringComparer.Ordinal).SequenceEqual(allowed.Order(StringComparer.Ordinal)))
            throw new InvalidOperationException($"{project}: unexpected {kind}: {string.Join(", ", actual)}");
    }

    private static void CheckPolicyRejectsForbiddenReferences()
    {
        foreach (var dependency in new[] { "Xcmd.Probe", "Avalonia.Desktop" })
        {
            try { RequireSame("deliberate violation", "reference", [dependency], []); }
            catch (InvalidOperationException) { continue; }
            throw new InvalidOperationException("The project policy missed a forbidden reference.");
        }
    }
}
