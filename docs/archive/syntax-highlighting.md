# Syntax highlighting

## Decision and boundaries

Use TextMateSharp and TextMateSharp.Grammars 2.0.5, pinned with their transitive
dependencies in NuGet lock files. The 3.x engine is still a preview at this
decision. TextMate provides broad language coverage and multiline lexical state.
It does not run compilers, language servers, extensions, or viewed code. No
grammars are downloaded at runtime.

A custom lexer for every language would create a large maintenance burden.
Tree-sitter is a possible future engine if syntax trees or incremental editing
become requirements. AvaloniaEdit would replace more of the existing reader's
selection, paging and search behavior than this feature requires.

| Layer | Responsibility |
| --- | --- |
| Core | `SyntaxDocument`, `SyntaxSpan`, `SyntaxKind`, and `SyntaxLanguage`: decoded text, UTF-16 ranges, style roles, grammar scopes, and fallback notices. `Select` clips ranges and makes offsets relative to the selection. |
| Infrastructure | `SyntaxGrammarCatalog` resolves names, extensions and common shebangs. `SyntaxHighlighter` reads context through `IViewerFiles` and runs TextMate away from the UI thread. `SyntaxScopes` maps scopes to application style roles. |
| Desktop | `SyntaxTextBox` retains TextBox input, selection and clipboard behavior. `SyntaxTextPresenter` creates colored Avalonia text runs. `SyntaxPalette` resolves roles against the reader theme with 4.5:1 foreground contrast. Selection foreground takes priority. |

Each viewer owns its language override and current syntax document. Existing
load cancellation prevents an old file, page or encoding result from replacing
newer state. A theme change reuses tokens. A mode change clears tokens. A new
file resets language detection. Archive members and Git materializations use
the same adapter boundary. Archive reports and binary, hex and image modes do
not use syntax highlighting. Colors do not change font metrics.

## Reading and limits

Automatic detection covers HTML, JSON/JSONC, Markdown, JavaScript/JSX,
TypeScript/TSX, Python, C#, Java, Kotlin, TOML, INI, XML, CSS, C/C++, PowerShell,
shell scripts and the other bundled languages. XML includes `.csproj`, `.props`
and `.targets`. Special names include Dockerfile and Makefile. Unknown types
remain plain text. View > Syntax language offers manual selection, Plain text
and Automatic.

Markdown remains source text with supported fenced-code highlighting. HTML can
include JavaScript and CSS. TextMateSharp does not support cross-grammar
injections; complete VS Code grammar compatibility and semantic analysis are
outside this feature. Highlighting does not validate source code.

The reader still displays 16 KiB pages. Tokenize decoded text from the start of
the file through the current page, then project ranges onto the page. This
preserves multiline comments, strings and embedded-language state, even when a
page starts inside a line or Unicode character. Context uses the selected
encoding and handles the BOM once.

Limit context to 1 MiB, lines to 8,192 UTF-16 characters, and the tokenization
loop to 1,500 ms with a 250 ms budget per line. Compile the bundled grammar
before starting the source-text budget; retain compiled grammars per viewer.
The engine's time budget is
cooperative: a native regex call cannot be forcibly cancelled. Cancellation is
checked between lines. These limits are not a process-isolation guarantee.
When a limit or engine failure prevents reliable highlighting, retain the text
and show an explicit plain-text notice. Do not guess distant multiline state.

Context is rebuilt for a new page so file edits and follow mode do not reuse
stale tokens. A future checkpoint cache must include file identity/version,
encoding and grammar revision, retain complete line state, and invalidate on
changes.

## Reports and future copy commands

Select affected code and choose Report highlighting problem in the context menu
or Help, or press Ctrl+Shift+F12. The existing comment form opens directly. It
shows the context before saving and asks what should change. Ctrl+Enter saves;
Escape cancels. Reports without a selection are also allowed.

The local SQLite issue contains the comment plus file name, language choice,
engine/grammar version, theme, encoding, page byte offset, selection range,
status and up to 4,096 selected characters. Unselected code is not included. No
network request is made. The stable target remains
`FileViewerWindow.TextContent`, and the issue keeps its application release.

Rich clipboard and image commands are deferred. Both must consume
`SyntaxDocument.Select(start, length)` and the same resolved palette as the
reader. Preserve characters, tabs and line breaks. Screen selection colors are
an interaction overlay and must not become exported code colors.

An HTML/RTF encoder can produce styled formats alongside plain text. An image
renderer can lay out the same selected document with explicit width, font,
background and scale. Keep these consumers apart from the OS clipboard adapter.
Do not capture the viewer window or depend on its scroll position. Export scope
is selected loaded text; cross-page selection is a separate reader feature.

## Grammar sources and updates

Engine and bundled grammars: [TextMateSharp](https://github.com/danipen/TextMateSharp),
NuGet 2.0.5, source commit `2db0b199c7cd96f889905ca1680a55246009bdc6`, MIT.
The 2.x engine uses Onigwrap 1.0.11; retain its native Windows x64 dependency in
published builds. Extra grammars are embedded as data with their license text:

| Grammar | Source | Pinned source |
| --- | --- | --- |
| Kotlin | [fwcd/vscode-kotlin](https://github.com/fwcd/vscode-kotlin/blob/main/syntaxes/kotlin.tmLanguage.json) | Git blob `9fded0ef09feccf7ed66cd449bf17e5da9b1d728`; MIT |
| TOML | [tamasfe/taplo](https://github.com/tamasfe/taplo/blob/08f343be02ce1b20296470396a42f0fa47820449/editors/vscode/toml.tmLanguage.json) | Commit `08f343be02ce1b20296470396a42f0fa47820449`; blob `8d1584616e8bcf3b264ca59d9e668d5f7053cec7`; MIT |

To add or update a language:

1. Pin the package or grammar revision and retain its license. Keep upstream
   grammar files unmodified. Update the catalog only when detection changes.
2. Increment `SyntaxHighlighter.Version` when embedded grammars change.
3. Add representative source and expected roles to `SyntaxProbe.Samples`. Turn
   reported bugs into small fixtures after removing private data.
4. Run the syntax scenarios and inspect their token JSON and offscreen images.
   Review changed roles against source; do not accept all changed expectations
   just because a new version produced them.
5. Update lock files and publish through the normal feedback-build workflow.

## Headless checks

- `syntax-language-grammars`: real files for the requested language families,
  meaningful tokens, embedded HTML/Markdown code and portable selection ranges.
- `syntax-page-context-and-limits`: multiline comments across 16 KiB boundaries,
  UTF-8/BOM and UTF-16, unknown types, long lines and context limits.
- `syntax-viewer-selection-and-report`: actual rendered colors in all nine
  themes, search, selection, manual language, reporting and SQLite persistence,
  and text/hex transitions.
- `syntax-report-current-selection-and-copy`: selection changes after closing a
  menu, plain-text Unicode copy, shortcut and pointer reports, cancellation and
  visible save confirmation.
- `syntax-scroll-and-replacement-during-load`: in-page scrolling preserves a
  pending syntax read; a replacement load cancels stale work.
- `syntax-follow-edits-and-rendering`: appended and truncated files rebuild
  syntax state; offscreen renders cover 100% and 150% scale.
- `syntax-archive-and-git-sources`: source from a real ZIP and a committed Git
  file uses the same highlighting path.
- `syntax-failure-cancellation-and-detection`: missing grammar fallback,
  cancellation, longest-suffix detection and case-insensitive XML aliases.
- Existing reader layout and scroll scenarios exercise the new text presenter.

Feedback command: `scripts/Publish-Feedback.ps1 -Filter 'syntax-,viewer-reading-menu-search-layout,viewer-continuous-scroll-end-and-status'`.
The [finalization record](evidence/syntax-verification-2026-11-15.md) records
24 passing selected scenarios, passing static checks and the independent
usability review. This is targeted verification, not a full regression run.
