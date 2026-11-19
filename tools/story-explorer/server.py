# @implements BrowseStories, TraceStoryFiles
"""Serve the story explorer on loopback without external dependencies."""

import argparse
import json
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlsplit

from repository import read_snapshot


STATIC_FILES = {
    "/": ("index.html", "text/html"),
    "/style.css": ("style.css", "text/css"),
    **{f"/{name}.js": (f"{name}.js", "text/javascript")
       for name in ("app", "reports", "charts", "treemap", "matrix", "matrix-order", "ui")},
}


class ExplorerServer(ThreadingHTTPServer):
    def __init__(self, root, port):
        self.root = root.resolve()
        self.snapshot = read_snapshot(self.root)
        self.scan_lock = threading.Lock()
        super().__init__(("127.0.0.1", port), ExplorerHandler)

    def refresh(self):
        with self.scan_lock:
            snapshot = read_snapshot(self.root)
            self.snapshot = snapshot
            return snapshot


class ExplorerHandler(BaseHTTPRequestHandler):
    def log_message(self, format_string, *args):
        pass

    def respond(self, status, body, content_type):
        data = body.encode("utf-8") if isinstance(body, str) else body
        self.send_response(status)
        self.send_header("Content-Type", content_type + "; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Security-Policy", "default-src 'self'; img-src 'self' data:; object-src 'none'; frame-ancestors 'none'")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        allowed_hosts = {f"127.0.0.1:{self.server.server_port}", f"localhost:{self.server.server_port}"}
        if self.headers.get("Host") not in allowed_hosts:
            self.respond(403, "Use the local explorer URL.", "text/plain")
            return
        url = urlsplit(self.path)
        if url.path in STATIC_FILES:
            filename, content_type = STATIC_FILES[url.path]
            self.respond(200, (Path(__file__).parent / filename).read_bytes(), content_type)
        elif url.path == "/api/catalogue":
            try:
                self.respond(200, json.dumps(self.server.refresh()), "application/json")
            except (ValueError, OSError, subprocess.SubprocessError) as error:
                self.respond(422, json.dumps({"error": str(error)}), "application/json")
        elif url.path == "/source":
            self.source(parse_qs(url.query).get("path", [""])[0])
        else:
            self.respond(404, "Not found.", "text/plain")

    def source(self, name):
        snapshot = self.server.snapshot
        allowed = set(snapshot["files"]) | {epic["path"] for epic in snapshot["epics"]}
        path = self.server.root / name
        if name not in allowed or not path.resolve().is_relative_to(self.server.root) or path.is_symlink():
            self.respond(404, "Source file not found in this map.", "text/plain")
            return
        try:
            self.respond(200, path.read_text(encoding="utf-8-sig"), "text/plain")
        except OSError:
            self.respond(404, "Source file is no longer available. Reload the map.", "text/plain")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--check", action="store_true", help="Validate the catalogue and source references, then exit.")
    args = parser.parse_args()
    if args.check:
        snapshot = read_snapshot(args.root)
        print(json.dumps({"counts": snapshot["counts"], "issues": snapshot["issues"]}, indent=2))
        return 1 if snapshot["issues"] else 0
    with ExplorerServer(args.root, args.port) as server:
        print(f"Story explorer: http://127.0.0.1:{server.server_port}", flush=True)
        print("Press Ctrl+C to stop. Reload the map to read current files.", flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
