# @implements ViewRecentAgentSessions
import json
import sys

try:
    from claude_agent_sdk import list_sessions
except ImportError:
    sys.exit("Install claude-agent-sdk in the Python environment used by Xcmd.")


def write_page(sessions):
    rows = [
        {
            "id": session.session_id,
            "topic": session.summary,
            "cwd": session.cwd,
            "updated": session.last_modified,
            "fileSize": getattr(session, "file_size", None),
        }
        for session in sessions
    ]
    print(json.dumps(rows), flush=True)


def main():
    for command in sys.stdin:
        command = command.strip()
        if command not in ("list", "recent"):
            sys.exit("Unknown session reader command.")
        sessions = list_sessions(limit=20 if command == "recent" else None)
        write_page(sessions[:20])
        for offset in range(20, len(sessions), 100):
            write_page(sessions[offset:offset + 100])
        print(flush=True)
        del sessions


if __name__ == "__main__":
    main()
