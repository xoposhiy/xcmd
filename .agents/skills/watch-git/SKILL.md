---
name: watch-git
description: run the full checks on the latest git commit and repair all detected problems. Call on проверь гит, watch git, repair git.
---

1. Get the latest changes from the git main
2. Run full set of tests, checks, linters, everything which is set up in the project.
3. Fix all the problems discovered by the checks
4. Commit fixes into main if any.

Start monitoring the git main branch for any further changes. If any — start this sequence again. If multimple new commits detected - handle the latest only.