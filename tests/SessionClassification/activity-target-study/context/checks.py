"""Offline regression checks for the context method."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent))
from evidence import powershell,trace_signals
from method import decide

def call(command,exit_code=0):
    return {"id":"s-L2","session":"s","tool":"CommandExecution","cwd":"/repo","windows":True,
            "command":command,"status":"completed","exit":exit_code,"changes":[],"excerpt":[],"output_lines":0}

def evidence(row):
    syntax=powershell([row])[row["id"]]
    return {"file_evidence":[],"cwd_is_checkout_root_at_inspection":True,"_statements":syntax["statements"],
            "_direct":{c["text"]:c["fileArgs"] for c in syntax["commands"] if c["hasPaths"] and not c["redirects"]}}

class ContextTests(unittest.TestCase):
    def test_quoted_paths_keep_their_distinct_roles(self):
        row=call("Get-Content -LiteralPath 'tests/a b.cs','src/main.cs'")
        guess=decide(row,evidence(row),[])
        self.assertEqual((guess["target"],guess["action"]),("mixed","read"))

    def test_multiple_queries_cover_both_objects(self):
        row=call("git status --short; Get-Content -LiteralPath README.md")
        guess=decide(row,evidence(row),[])
        self.assertEqual((guess["target"],guess["action"]),("mixed","read"))

    def test_parsing_never_runs_a_recorded_command(self):
        with tempfile.TemporaryDirectory() as directory:
            marker=Path(directory)/"sentinel.txt"
            row=call("New-Item '"+str(marker)+"'; Get-Content README.md")
            evidence(row)
            self.assertFalse(marker.exists())

    def test_request_received_during_a_command_cannot_supply_context(self):
        with tempfile.TemporaryDirectory() as directory:
            p=Path(directory)/"trace.jsonl"
            records=[{"type":"session_meta","payload":{"cwd":directory}},
                {"type":"event_msg","timestamp":"1970-01-01T00:00:01Z","payload":{"type":"user_message","message":"Read the documentation"}},
                {"type":"event_msg","timestamp":"1970-01-01T00:00:03Z","payload":{"type":"user_message","message":"Run the tests"}},
                {"type":"event_msg","payload":{"type":"item_completed","started_at_ms":2000,"item":{"type":"CommandExecution","cwd":directory}}}]
            p.write_text("\n".join(map(json.dumps,records)),encoding="utf8")
            _,signals=trace_signals({"key":"s","repo":directory,"trace":str(p)})
            self.assertEqual(signals["s-L4"]["last_user_request_topics"],["documentation"])

if __name__=="__main__":unittest.main()
