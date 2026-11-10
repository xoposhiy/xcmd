using System.Diagnostics;

namespace Xcmd.Architecture.Infrastructure;

internal static class AnalyzerChecks
{
    public static async Task CheckAsync(string root)
    {
        var folder = Path.Combine(root, "artifacts", "architecture", "analyzers", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var policy = await File.ReadAllTextAsync(Path.Combine(root, ".editorconfig"));
        await File.WriteAllTextAsync(Path.Combine(folder, ".editorconfig"), policy.Replace("[src/**/*.cs]", "[*.cs]", StringComparison.Ordinal));
        var project = Path.Combine(folder, "Xcmd.Core.csproj");
        await File.WriteAllTextAsync(project, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.Copy(Path.Combine(root, "src", "Xcmd.Core", "packages.lock.json"), Path.Combine(folder, "packages.lock.json"));
        var restore = await RunAsync(folder, "restore", project, "-r", "win-x64", "-p:Configuration=Release", "--configfile", Path.Combine(root, "NuGet.Config"), "--locked-mode", "--disable-parallel");
        if (restore.ExitCode != 0) throw new InvalidOperationException(restore.Output);
        await File.WriteAllTextAsync(Path.Combine(folder, "Allowed.cs"), "public static class Allowed { public static async Task<string> ReadAsync(string path, CancellationToken token) { await Task.Delay(1, token); return Path.GetFileName(path); } }");
        var positive = await BuildAsync(folder, project);
        if (positive.ExitCode != 0) throw new InvalidOperationException("The API policy rejects pure paths or awaited work: " + positive.Output);

        (string Name, string Body, string Diagnostic)[] cases =
        [
            ("FileApi", "public static bool Run() => System.IO.File.Exists(\"x\");", "RS0030"),
            ("ProcessApi", "public static int Run() => System.Diagnostics.Process.GetCurrentProcess().Id;", "RS0030"),
            ("ClockApi", "public static DateTime Run() => DateTime.UtcNow;", "RS0030"),
            ("WorkingDirectory", "public static string Run() => Environment.CurrentDirectory;", "RS0030"),
            ("EnvironmentRead", "public static string? Run() => Environment.GetEnvironmentVariable(\"XCMD_TEST\");", "RS0030"),
            ("SystemFolder", "public static string Run() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);", "RS0030"),
            ("BlockingWait", "public static bool Run(Task task) => task.Wait(TimeSpan.FromSeconds(1));", "RS0030"),
            ("BlockingResult", "public static int Run(Task<int> task) => task.Result;", "RS0030"),
            ("BlockingAwaiter", "public static int Run(Task<int> task) => task.ConfigureAwait(false).GetAwaiter().GetResult();", "RS0030"),
            ("BlockingSleep", "public static void Run() => Thread.Sleep(TimeSpan.FromMilliseconds(1));", "RS0030"),
            ("NativeInterop", "[System.Runtime.InteropServices.DllImport(\"native\")] public static extern int Run();", "RS0030"),
            ("Cancellation", "public static async Task Run(CancellationToken cancellationToken) { await Task.Delay(1); }", "CA2016"),
            ("Comparison", "public static int Run(string left, string right) => string.Compare(left, right);", "CA1309"),
            ("Complexity", "public static int Run(int value) { " + string.Concat(Enumerable.Range(0, 30).Select(index => $"if (value == {index}) return {index}; ")) + "return -1; }", "CA1502")
        ];
        foreach (var (name, body, _) in cases)
            await File.WriteAllTextAsync(Path.Combine(folder, name + ".cs"), $"public static class {name} {{ {body} }}");
        var negative = await BuildAsync(folder, project);
        await File.WriteAllTextAsync(Path.Combine(folder, "diagnostics.txt"), negative.Output);
        if (negative.ExitCode == 0) throw new InvalidOperationException("The compiler accepted deliberate architecture violations.");
        foreach (var (name, _, diagnostic) in cases)
            if (!negative.Output.Split('\n').Any(line => line.Contains(name + ".cs(", StringComparison.Ordinal) && line.Contains("error " + diagnostic, StringComparison.Ordinal)))
                throw new InvalidOperationException($"{name}: expected {diagnostic}. See {folder}/diagnostics.txt");
        Console.WriteLine($"PASS analyzer checks ({cases.Length} deliberate violations and allowed pure paths/async work)");
    }

    private static Task<(int ExitCode, string Output)> BuildAsync(string folder, string project) =>
        RunAsync(folder, "build", project, "-c", "Release", "-r", "win-x64", "--no-restore", "-m:1", "-p:UseSharedCompilation=false", "-v:minimal");

    private static async Task<(int ExitCode, string Output)> RunAsync(string folder, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("The analyzer check could not start dotnet.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output + await error);
    }
}
