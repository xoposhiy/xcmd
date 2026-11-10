# Technology decision: C# and Avalonia

Status: Accepted by the user on 2026-11-09.

## Decision

Build Xcmd as a Windows desktop application with C# on .NET and Avalonia UI. Use Avalonia's core `TableView` for the two filesystem panels. Keep dependencies pinned and retain package lock files. The SDK is selected by `global.json`; the Avalonia version is selected by `Directory.Build.props`.

Use application code for file-manager sorting, cursor state, selection preservation and inline rename. The current panels need no paid UI components. Any future paid component needs a separate decision. Avalonia's other platforms do not add cross-platform delivery to the product scope.

## Reasons

- C# supports typed application rules, asynchronous work, automated tests and Windows APIs.
- Avalonia supplies controls, row virtualization, input routing, styling and layout.
- Avalonia.Headless drives the actual application controls without native windows. Skia renders the same interface into memory for layout checks.
- The accepted application demonstrated real directory loading, keyboard selection, dialogs, file operations, offscreen rendering and clean shutdown without human assistance.

## Architecture and evidence

Keep application behavior separate from filesystem access, Windows integration, rendering and input transport. Keep scenario cases separate from test drivers and fixtures. Use the same controls, handlers and services in interactive and headless operation.

The [code architecture](architecture.md) defines the three production projects. The [development requirements](development-requirements.md) define behavioral evidence and ready-build delivery. The [automated checks](automated-checks.md) enforce dependencies, API boundaries, formatting and code metrics. Current evidence and native limits are mapped in [verification](verification.md).

Stack acceptance applies to the implemented workflows. It does not establish coverage for planned features. Retain the panel regression scenarios when changing controls or dependencies. Reconsider a control or adapter when measured problems require it; a new feature alone does not require reopening the stack choice.
