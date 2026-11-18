# Source code

- [Xcmd.Core](Xcmd.Core/) — **App logic and interfaces independent of the UI and operating system** (for example, deciding whether a file name matches a search).
- [Xcmd.Infrastructure](Xcmd.Infrastructure/) — **Adapters for external systems** (for example, reading ZIP archives and sending files to the Windows Recycle Bin).
  - [Syntax](Xcmd.Infrastructure/Syntax/) — **Code and language definitions for syntax highlighting**.
- [Xcmd.Desktop](Xcmd.Desktop/) — **User interface and input handling**.
  - [Assets](Xcmd.Desktop/Assets/) — **App icons**.
  - [config](Xcmd.Desktop/config/) — **Default file type settings**.
