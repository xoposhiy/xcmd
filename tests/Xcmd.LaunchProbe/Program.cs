using System.Text.Json;

var inspectEnvironment = args.FirstOrDefault() == "--environment";
var editorMode = File.Exists(Path.Combine(AppContext.BaseDirectory, "editor-probe.mode"));
var path = inspectEnvironment ? args[1] : editorMode || args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "launch-report.json") : args[0];
var report = new
{
    processId = Environment.ProcessId,
    workingDirectory = Environment.CurrentDirectory,
    arguments = args.Skip(inspectEnvironment ? 2 : editorMode ? 0 : 1).ToArray(),
    executable = Environment.ProcessPath,
    environment = inspectEnvironment ? args.Skip(2).ToDictionary(name => name, Environment.GetEnvironmentVariable) : null
};
var temporary = path + ".tmp";
await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report));
File.Move(temporary, path, overwrite: true);
return 0;
