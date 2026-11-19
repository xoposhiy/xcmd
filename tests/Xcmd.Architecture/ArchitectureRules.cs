// @implements QualityChecks
using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Slices;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using SliceIdentifier = ArchUnitNET.Domain.SliceIdentifier;

namespace Xcmd.Architecture;

internal static class ArchitectureRules
{
    private const string UiNamespaces = "^Avalonia($|\\.)";

    public static (string Name, IArchRule Rule)[] Production() =>
    [
        ("Core has no UI or adapter dependencies", NoDependencies("Xcmd.Core", "^(Xcmd\\.(Desktop|Infrastructure|Probe)|Avalonia|Microsoft\\.Win32)($|\\.)")),
        ("Infrastructure has no UI dependencies", NoDependencies("Xcmd.Infrastructure", "^(Xcmd\\.(Desktop|Probe)|Avalonia)($|\\.)")),
        ("Production has no test dependencies", Types().That().ResideInNamespaceMatching("^Xcmd\\.(Core|Infrastructure|Desktop)($|\\.)")
            .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching("^Xcmd\\.(Probe|LaunchProbe|Architecture)($|\\.)"))),
        ("Test infrastructure has no scenario dependencies", NoNamespaceDependencies("^Xcmd\\.Probe\\.Infrastructure($|\\.)", "^Xcmd\\.Probe\\.Scenarios($|\\.)")),
        ("Scenarios keep native and headless setup in infrastructure", NoNamespaceDependencies("^Xcmd\\.Probe\\.Scenarios($|\\.)", "^(System\\.(Diagnostics|Runtime\\.InteropServices)|Avalonia\\.Headless)($|\\.)")),
        ("Projects have no dependency cycles", NoProjectCycles()),
        ("Core is included", Types().That().ResideInAssembly(typeof(Core.PanelSession).Assembly.FullName!).Should().Exist()),
        ("Infrastructure is included", Types().That().ResideInAssembly(typeof(Xcmd.Infrastructure.LocalFileSystem).Assembly.FullName!).Should().Exist()),
        ("Desktop is included", Types().That().ResideInAssembly(typeof(Desktop.Workspace.CommanderWindow).Assembly.FullName!).Should().Exist()),
        ("Scenario group is included", Types().That().ResideInNamespaceMatching("Xcmd.Probe.Scenarios").Should().Exist()),
        ("Test infrastructure group is included", Types().That().ResideInNamespaceMatching("Xcmd.Probe.Infrastructure").Should().Exist())
    ];

    private static IArchRule NoProjectCycles()
    {
        var rule = new SliceRuleCreator();
        rule.SetSliceAssignment(new(type => type.Assembly.Name.StartsWith("Xcmd.", StringComparison.Ordinal)
            ? SliceIdentifier.Of(type.Assembly.Name, null, "") : SliceIdentifier.Ignore(), "by Xcmd assembly"));
        return new GivenSlices(rule).Should().BeFreeOfCycles();
    }

    public static IArchRule NoDependencies(string assembly, string forbiddenNamespaces) =>
        Types().That().ResideInAssembly(Assembly.Load(assembly).FullName!).Should()
            .NotDependOnAny(Types(true).That().ResideInNamespaceMatching(forbiddenNamespaces));

    public static IArchRule NoNamespaceDependencies(string sourceNamespaces, string forbiddenNamespaces) =>
        Types().That().ResideInNamespaceMatching(sourceNamespaces).Should()
            .NotDependOnAny(Types(true).That().ResideInNamespaceMatching(forbiddenNamespaces));

    public static IArchRule NoUiDependencies(string sourceNamespaces) => NoNamespaceDependencies(sourceNamespaces, UiNamespaces);
}
