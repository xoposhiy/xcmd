---
id: ReusableBoundaries
title: "Implement workflows over reusable boundaries"
order: 10
role: developer
---

# Implement workflows over reusable boundaries

As a developer, I want to implement workflows over explicit domain and platform
boundaries so that new stories can reuse file, search, repository, and presentation
services.

[All epics](README.md)

## DomainBoundaries

As a developer, I want to implement domain behavior independently of the UI and
operating system so that I can reason about rules apart from external operations.

## FileSystemAccess

As a developer, I want to read, change, compare, and count filesystem entries through a
common adapter so that I can implement file workflows without duplicating OS access.

## WindowsFileActions

As a developer, I want to call native copy, recycle, menus, properties, and drag and
drop behind managed interfaces so that I can extend file actions without spreading COM
and native ownership rules.

## DirectoryMonitoring

As a developer, I want to receive bounded directory-change notifications and inspect
availability so that I can refresh views without coupling them to watcher details.

## ExternalLaunch

As a developer, I want to resolve and launch external tools through explicit adapters so
that I can add entry workflows without duplicating Windows process rules.

## ArchiveAccess

As a developer, I want to read and create archive content through common interfaces so
that I can reuse file workflows for packaged content.

## SearchSources

As a developer, I want to search physical/archive text and explain Git ignore decisions
through one source so that I can evolve search scheduling without embedding storage or
Git parsing in Core.

## RepositoryStatus

As a developer, I want to read repository and child-repository status without Git write
side effects so that I can expose repository context through stable models.

## RepositoryLocations

As a developer, I want to read scoped logs, trees, changes, and messages through a read-
only filesystem so that I can reuse panel navigation for repository history.

## RepositoryDocuments

As a developer, I want to present repository contents and changes in the existing viewer
so that I can reuse inspection workflows while preserving their origin and read-only
state.

## StreamingReads

As a developer, I want to read file windows and search full files through reusable
streaming services so that I can keep viewer features independent of file size and
encoding details.

## SyntaxSpans

As a developer, I want to produce portable syntax spans and render them with readable
colors so that I can extend highlighting without rewriting the viewer.

## SharedControls

As a developer, I want to compose controls with shared layout, styling, and list
rendering so that I can add workflows without copying presentation mechanics.

## FileIcons

As a developer, I want to resolve system icons with bounded caching and correct native
ownership so that I can show recognizable files without blocking list rendering.

## ClipboardInterop

As a developer, I want to read and write file lists and preferred drop effects through
one adapter so that I can reuse clipboard interoperability apart from transfer rules.

## ImageDecoding

As a developer, I want to turn supported image bytes into a displayable bitmap with
resource limits so that I can reuse image preview without spreading codec details.

## AsyncCoordination

As a developer, I want to compose services and route actions while keeping cancellation
and pending work explicit so that I can add workflows with visible process and UI
ownership.

## KeyboardLayouts

As a developer, I want to translate physical strokes and text through installed Windows
layouts so that I can add layout-aware typing features without duplicating native input
handling.
