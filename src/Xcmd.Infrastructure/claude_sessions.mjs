// @implements ViewRecentAgentSessions
import { createRequire } from "node:module";
import { join } from "node:path";
import { createInterface } from "node:readline";
import { pathToFileURL } from "node:url";

const sdk = createRequire(join(process.argv[2], "package.json"));
const { listSessions } = await import(pathToFileURL(sdk.resolve("@anthropic-ai/claude-agent-sdk")));

function writePage(sessions) {
    const rows = sessions.map(({ sessionId, summary, cwd, lastModified, fileSize }) =>
        ({ id: sessionId, topic: summary, cwd: cwd ?? "", updated: lastModified, fileSize }));
    process.stdout.write(JSON.stringify(rows) + "\n");
}

for await (const command of createInterface({ input: process.stdin })) {
    if (command !== "list" && command !== "recent") throw new Error("Unknown session reader command.");
    const recent = await listSessions({ limit: 20 });
    writePage(recent);
    // A session can move across the page boundary between the two reads.
    if (command === "list" && recent.length === 20) {
        const history = await listSessions();
        for (let offset = 0; offset < history.length; offset += 100) {
            writePage(history.slice(offset, offset + 100));
        }
    }
    process.stdout.write("\n");
}
