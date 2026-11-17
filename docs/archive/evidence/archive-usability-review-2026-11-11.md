# Archive workflow usability review

Reviewed on 2026-11-11. Scope: UC21 and archive-related viewing and extraction.

An independent reviewer inspected the actual source, scenario cases and offscreen captures. The reviewer did not change files, run builds or open visible windows. The review used these criteria: frequent commands need few actions and clear results; rare actions explain their effects; rare scenarios can be found in the application.

## Findings and resolutions

| Finding | Resolution |
| --- | --- |
| Completing a slow archive read could take focus from the other panel. | Restore focus only when the source panel still owns the file interaction. A controlled delayed-read scenario switches panels before completion. |
| Opening a slow archive had no visible pending state or direct cancellation. | Show the archive name and `Escape cancels`. Cancellation keeps the original location, cursor and command draft and rejects obsolete results. |
| Binary and Hex could show report bytes as archive bytes. | Identify archive reports in the title and reading status. Disable Binary, Hex and Image for reports, including keyboard mode changes. |
| Opening an archive through Windows was difficult to discover. | Add File > Open with Windows. Keep the existing Ctrl+Enter followed by Enter workflow. |

The independent follow-up review found no open usability findings in the revised source. These fixes require no new product decisions.

## Runtime evidence

The implementing agent ran all nine archive scenarios after the fixes. All passed with zero native application windows. The scenarios cover actual keyboard and menu routing, controlled pending reads, cancellation, report identity, file and folder extraction, Skip/Replace conflicts, cancellation after a real decompressed block was written, preservation of the existing destination, temporary-file cleanup, refreshed archive contents, parent navigation and workspace restore.

The format scenario reads real ZIP, RAR4/5, 7z, 7z SFX, TAR, TAR.GZ, GZip, BZip2, XZ, TAR.BZ2 and TAR.XZ samples. Tests never execute archive programs. Final verification results are recorded separately in the release evidence.

## Limits

A source-only follow-up reviewed automatic archive detection after the full suite found that zero-filled binary data could be treated as an empty TAR. Automatic F3 now retains the byte view for an unknown extension with an all-zero TAR header. An explicit `.tar` or Ctrl+PageDown still allows empty archive browsing. The independent reviewer found no additional usability issues. Regression checks cover ordinary binary data, explicit empty TAR reports and the forced archive route.

A source-only follow-up reviewed automatic archive detection after the full suite found that zero-filled binary data could be treated as an empty TAR. Automatic F3 now retains the byte view for an unknown extension with an all-zero TAR header. An explicit `.tar` or Ctrl+PageDown still allows empty archive browsing. The independent reviewer found no additional usability issues. Regression checks cover ordinary binary data, explicit empty TAR reports and the forced archive route.

The review does not establish large-archive opening latency or efficient bulk extraction of sequential and solid archives. Native Windows presentation, third-party archive applications, password entry and multipart discovery remain outside this evidence. The controlled cancellation checks verify event order and state rather than fragile time thresholds.
