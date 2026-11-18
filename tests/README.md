# Tests

- [Xcmd.Probe](Xcmd.Probe/) — **App behavior tests and a runner that works without opening windows**.
  - [Scenarios](Xcmd.Probe/Scenarios/) — **Test cases** (for example, checking that confirming a copy puts the file in the folder shown in the other panel).
  - [Infrastructure](Xcmd.Probe/Infrastructure/) — **Test setup and helpers** (for example, creating temporary folders and simulating key presses).
  - [Fixtures](Xcmd.Probe/Fixtures/) — **Sample archives used as test inputs**.
- [Xcmd.Architecture](Xcmd.Architecture/) — **Checks for how the code is organized** (for example, which projects can depend on each other).
  - [Infrastructure](Xcmd.Architecture/Infrastructure/) — **Helpers that inspect projects and verify that the checks report problems**.
  - [Samples](Xcmd.Architecture/Samples/) — **Sample code that breaks architecture rules on purpose**.
- [Xcmd.LaunchProbe](Xcmd.LaunchProbe/) — **Helper program for tests that need a second process** (for example, it keeps a file open while Xcmd tries to rename it).
