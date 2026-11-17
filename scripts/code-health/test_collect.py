# @tests CodeHealth
"""Check report aggregation and source selection with small fixtures."""

import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

from collect import assess_verification, collect_roslyn, enrich, fingerprint, project_order, source_files, summarize


def function(path, ccn, kind="function"):
    return {"path": path, "project": "Xcmd.Core", "kind": kind, "ccn": ccn,
            "cognitive": 2, "nesting": 1, "codeLines": 4, "parameters": 1,
            "utilization": ccn / 25, "violations": []}


def file(path, lines=10):
    return {"path": path, "project": "Xcmd.Core", "scope": "production",
            "folder": str(Path(path).parent).replace("\\", "/"),
            "physicalLines": lines, "codeLines": lines}


class AggregationTests(unittest.TestCase):
    def test_average_uses_functions_instead_of_file_averages(self):
        functions = [function("first.cs", 1), function("first.cs", 3), function("second.cs", 20)]
        result = summarize([file("first.cs"), file("second.cs")], functions, [], [])
        self.assertEqual(8, result["averageCcn"])
        self.assertEqual(24, result["totalCcn"])

    def test_parser_blocks_are_not_counted_as_functions(self):
        result = summarize([], [function("first.cs", 2), function("first.cs", 24, "parser-block")], [], [])
        self.assertEqual(1, result["functions"])
        self.assertEqual(1, result["parserBlocks"])
        self.assertEqual(2, result["maxCcn"])

    def test_empty_groups_keep_missing_metrics_distinct_from_zero(self):
        result = summarize([], [], [], [])
        self.assertIsNone(result["averageCcn"])
        self.assertIsNone(result["roslynMinMemberMi"])
        self.assertEqual(0, result["files"])

    def test_partial_type_is_counted_once_in_project_summary(self):
        files = [file("src/Xcmd.Core/First.cs"), file("src/Xcmd.Core/Second.cs")]
        types = [{"project": "Xcmd.Core", "paths": [row["path"] for row in files], "coupling": 9}]
        projects, folders = enrich(files, [], [{"name": "Xcmd.Core", "maintainability": 90}], [], types)
        self.assertEqual(1, projects[0]["summary"]["roslynTypes"])
        self.assertEqual(9, projects[0]["summary"]["roslynMaxTypeCoupling"])
        self.assertEqual(1, folders[0]["summary"]["roslynTypes"])

    def test_member_mi_is_the_minimum_not_a_synthetic_file_score(self):
        result = summarize([], [], [{"kind": "Method", "maintainability": 44, "coupling": 3},
                                    {"kind": "Method", "maintainability": 80, "coupling": 8}], [])
        self.assertEqual(44, result["roslynMinMemberMi"])
        self.assertEqual(8, result["roslynMaxMemberCoupling"])


class SourceTests(unittest.TestCase):
    def test_fingerprint_includes_build_profile_launcher_lock_and_release_scripts(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name in (".editorconfig", "Directory.Build.props", "Directory.Build.targets", "global.json", "CodeMetricsConfig.txt", "Xcmd.slnx", "NuGet.Config",
                         "config/metrics.json", "src/Xcmd.Infrastructure/packages.launcher.lock.json", "scripts/Build-Launcher.ps1", "scripts/Install-Launcher.ps1", "scripts/Release-Xcmd.ps1"):
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("fixture")
            before = fingerprint(root)
            for name in ("Directory.Build.targets", "src/Xcmd.Infrastructure/packages.launcher.lock.json", "scripts/Build-Launcher.ps1", "scripts/Install-Launcher.ps1", "scripts/Release-Xcmd.ps1"):
                (root / name).write_text("changed")
                after = fingerprint(root)
                self.assertNotEqual(before, after)
                before = after

    def test_metric_projects_load_dependencies_before_their_consumers(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            core = root / "src/Xcmd.Core/Xcmd.Core.csproj"
            desktop = root / "src/Xcmd.Desktop/Xcmd.Desktop.csproj"
            core.parent.mkdir(parents=True)
            desktop.parent.mkdir(parents=True)
            core.write_text('<Project />')
            desktop.write_text('<Project><ItemGroup><ProjectReference Include="../Xcmd.Core/Xcmd.Core.csproj" /></ItemGroup></Project>')
            self.assertEqual([core.resolve(), desktop.resolve()], project_order(root))

    def test_build_output_is_excluded_from_source_files(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for path in ("src/Xcmd.Core/Rule.cs", "src/Xcmd.Core/obj/Generated.cs", "tests/Xcmd.Probe/bin/Copy.cs", "tests/Xcmd.Probe/Scenarios/Case.cs"):
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("class Example {}")
            self.assertEqual(["src/Xcmd.Core/Rule.cs", "tests/Xcmd.Probe/Scenarios/Case.cs"],
                             [path.relative_to(root).as_posix() for path in source_files(root)])

    def test_roslyn_generated_members_do_not_enter_file_summaries(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source = root / "src/Xcmd.Core/Rule.cs"
            source.parent.mkdir(parents=True)
            source.write_text("class Rule {}")
            report = ET.Element("CodeMetricsReport")
            target = ET.SubElement(ET.SubElement(report, "Targets"), "Target", Name="Xcmd.Core.csproj")
            assembly = ET.SubElement(target, "Assembly", Name="Xcmd.Core")
            namespace = ET.SubElement(ET.SubElement(assembly, "Namespaces"), "Namespace", Name="Xcmd.Core")
            type_ = ET.SubElement(ET.SubElement(namespace, "Types"), "NamedType", Name="Rule")
            members = ET.SubElement(type_, "Members")
            for path in (source, root / "src/Xcmd.Core/obj/Generated.cs"):
                member = ET.SubElement(members, "Method", Name="Run", File=str(path), Line="1")
                ET.SubElement(ET.SubElement(member, "Metrics"), "Metric", Name="MaintainabilityIndex", Value="50")
            xml = root / "metrics.xml"
            ET.ElementTree(report).write(xml)
            _, symbols, types = collect_roslyn(root, xml, [source])
            self.assertEqual(1, len(symbols))
            self.assertEqual(["src/Xcmd.Core/Rule.cs"], types[0]["paths"])


class VerificationTests(unittest.TestCase):
    def test_current_passing_run_keeps_passed_status(self):
        result = assess_verification({"status": "passed", "sourceFingerprintBefore": "same", "sourceFingerprintAfter": "same"}, "same")
        self.assertEqual("passed", result["status"])
        self.assertTrue(result["matchesCurrentSource"])

    def test_source_change_prevents_current_passing_status(self):
        result = assess_verification({"status": "passed", "sourceFingerprintBefore": "old", "sourceFingerprintAfter": "old"}, "new")
        self.assertEqual("source-changed", result["status"])
        self.assertFalse(result["matchesCurrentSource"])

    def test_matching_source_does_not_turn_skipped_checks_into_passed_checks(self):
        result = assess_verification({"status": "skipped", "sourceFingerprintBefore": "same", "sourceFingerprintAfter": "same"}, "same")
        self.assertEqual("skipped", result["status"])

    def test_matching_source_does_not_hide_a_failed_run(self):
        result = assess_verification({"status": "failed", "sourceFingerprintBefore": "same", "sourceFingerprintAfter": "same"}, "same")
        self.assertEqual("failed", result["status"])


if __name__ == "__main__":
    unittest.main()
