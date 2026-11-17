// @tests QualityChecks
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

namespace Xcmd.Architecture;

internal static class ArchitectureRuleChecks
{
    public static void Check()
    {
        var sample = new ArchLoader().LoadAssemblies(typeof(ArchitectureRuleChecks).Assembly).Build();
        MustFail(ArchitectureRules.NoUiDependencies("Xcmd.Architecture.Samples.Core"));
        MustFail(ArchitectureRules.NoNamespaceDependencies("Xcmd.Architecture.Samples.Infrastructure", "Xcmd.Architecture.Samples.Desktop"));
        MustFail(ArchitectureRules.NoNamespaceDependencies("Xcmd.Architecture.Samples.Production", "Xcmd.Architecture.Samples.Scenarios"));
        MustFail(ArchitectureRules.NoNamespaceDependencies("Xcmd.Architecture.Samples.TestInfrastructure", "Xcmd.Architecture.Samples.Scenarios"));
        MustFail(ArchitectureRules.NoNamespaceDependencies("Xcmd.Architecture.Samples.Scenarios", "System.Diagnostics"));
        MustFail(ArchUnitNET.Fluent.Slices.SliceRuleDefinition.Slices().Matching("Xcmd.Architecture.Samples.(*)").Should().BeFreeOfCycles());
        MustFail(ArchUnitNET.Fluent.ArchRuleDefinition.Types().That().ResideInNamespace("MissingGroup").Should().Exist());

        void MustFail(IArchRule rule)
        {
            if (rule.HasNoViolations(sample)) throw new InvalidOperationException("An architecture rule missed a deliberate violation: " + rule);
        }
    }
}
