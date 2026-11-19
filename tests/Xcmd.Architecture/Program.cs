// @implements QualityChecks
using ArchUnitNET.Loader;
using Xcmd.Architecture.Infrastructure;

namespace Xcmd.Architecture;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(args.Single());
            await ProjectInspection.CheckAsync(root);
            var architecture = new ArchLoader().LoadAssemblies(
                typeof(Core.PanelSession).Assembly,
                typeof(Xcmd.Infrastructure.LocalFileSystem).Assembly,
                typeof(Desktop.Workspace.CommanderWindow).Assembly,
                typeof(Probe.Infrastructure.ProbeContext).Assembly).Build();
            var rules = ArchitectureRules.Production();
            foreach (var (name, rule) in rules)
            {
                var violations = rule.Evaluate(architecture).Where(result => !result.Passed).ToArray();
                if (violations.Length > 0)
                    throw new InvalidOperationException(name + ": " + string.Join(Environment.NewLine, violations.Select(result => result.Description)));
                Console.WriteLine("PASS " + name);
            }
            ArchitectureRuleChecks.Check();
            await AnalyzerChecks.CheckAsync(root);
            Console.WriteLine($"Architecture: {rules.Length} rules and deliberate-violation checks passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
