---
id: ArchiveWork
title: "Work with packaged files"
order: 5
role: user
---

# Work with packaged files

As a user, I want to inspect, unpack, and package files so that I can work with content
delivered as archives.

[All epics](README.md)

## BrowseArchives

As a user, I want to enter supported archives and nested members so that I can inspect
their structure before extracting.

### Implementation decisions

- Enter opens a recognized archive as a filesystem. Ctrl+PgDown tries to enter a file as
  an archive regardless of extension, including archive-based Office documents and
  self-extracting executables. Ctrl+Enter followed by Enter remains a way to open the
  archive through Windows Shell.
  Source: [2026-11-11T10-04-57-01a1c9da-6ac4-7363-bb58-482d3573c0d5.md → Turn 3](../codex-sessions/2026-11-11T10-04-57-01a1c9da-6ac4-7363-bb58-482d3573c0d5.md#turn-3).

## InspectArchives

As a user, I want to view archive information or open an individual member so that I can
understand packaged content without a full extraction.

### Implementation decisions

- F3 on an archive shows its contents and available archive information. Support common
  archive formats that can be supported readily; the user did not select a particular
  archive library.
  Source: [2026-11-11T10-04-57-01a1c9da-6ac4-7363-bb58-482d3573c0d5.md → Turn 3](../codex-sessions/2026-11-11T10-04-57-01a1c9da-6ac4-7363-bb58-482d3573c0d5.md#turn-3).

## ExtractEntries

As a user, I want to copy archive files or folders to a regular directory so that I can
use only the packaged content I need.

## CreateArchive

As a user, I want to package selected files into an archive so that I can share or store
the selection as one file.
