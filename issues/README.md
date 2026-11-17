# Local issue store

`issues.db` stores in-app feedback independently of saved workspaces. It is shared
by the application and development agents and is committed with the project.

Follow the [feedback process](../docs/processes/feedback.md) to list, claim and
finish work through `scripts/Issues.ps1`. Do not edit or replace the live database
directly. Do not delete SQLite journals while a transaction is active.

The previous implementation description is [archived](../docs/archive/issue-tracker.md).
