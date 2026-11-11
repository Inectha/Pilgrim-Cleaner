# AGENTS.md — instructions for AI coding agents (and humans)

This file is read by Google Antigravity, Claude Code (via `CLAUDE.md`), Cursor and other agents.
Keep it the **single source** of project rules: change it here, not in tool-specific files.

## What this project is

Pilgrim Cleaner: a Windows disk cleaner, app uninstaller and sysadmin CLI.
- `PilgrimCleaner.csproj`: WPF GUI (WPF-UI, CommunityToolkit.Mvvm), .NET 10 (`net10.0-windows`)
- `cli/`: `pilgrim.exe` CLI, references the GUI project
- `Tests/`: test console app (see Testing)
- Namespace is `LuminaCleaner` (old project name). Do not rename it as part of unrelated work.

**This tool deletes files, uninstalls software and runs as administrator; the CLI is meant to run as SYSTEM.**
A bug here can destroy user data or let a normal user gain admin rights. Treat every change to
deletion, process launching, registry access, installer or release code as security-sensitive.

## Build and test

```powershell
dotnet build PilgrimCleaner.csproj
dotnet build cli/PilgrimCli.csproj
dotnet run --project Tests/PilgrimCleaner.Tests.csproj      # must end with "ALL TESTS PASSED"
```
- Builds must have **0 warnings**.
- CI (`.github/workflows/ci.yml`) runs the same on every pull request, as administrator, plus an installer build. It must be green before merging.
- Never run `-- --user-audit` on a real machine: it registers a real scheduled task and toggles a real startup entry. VM / Windows Sandbox only.
- Never run `pilgrim clean`, uninstall or duplicate deletion on your own machine to "try it out". Use `pilgrim audit` (read-only) or `--dry-run`.

## Safety rules (non-negotiable)

1. **Deleting files**
   - Only through `CleanerModuleBase.SafeEnumerateFiles` + `SafeDeleteFile` (which use `Interop/SafeFileOps`). They refuse junctions and symlinks planted by normal users, and verify each file's real location before deleting it.
   - Never add `File.Delete`, `Directory.Delete(path, true)` or `SearchOption.AllDirectories` on paths that come from a scan, the registry or user input.
   - Never follow reparse points (junctions or symlinks) when walking folders.
2. **Recycle Bin**: use `NativeMethods.SendToRecycleBin`. It refuses network and USB drives, where "recycle" would silently mean "delete permanently".
3. **Registry data is untrusted.**
   - Anything under `HKEY_CURRENT_USER` can be written by any program the user runs. Never execute it with administrator rights (no `Verb = "runas"`).
   - Never use registry values as file paths without validating them. See `AppManagerService.IsSafeFolderName` / `IsPathSafeForLeftoverTarget`.
4. **Starting processes**
   - Use the absolute paths in `Interop/SystemTools` (`SystemTools.PowerShell`, `.Schtasks`, `.Dism`, …). Never use a bare `"powershell.exe"`, because a planted copy in the current folder would run as SYSTEM.
   - Never build a PowerShell or cmd command string from external data (file names, update titles, registry values). Pass data as Base64 and the script via `-EncodedCommand`; see `DriverHealthService.BuildDriverInstallScript`.
5. **Destructive CLI commands**
   - They must ask for confirmation, or require `--force` when unattended or with `--json`.
   - `--dry-run` must never change anything.
6. **Cleaning policy** (from CONTRIBUTING.md)
   - Keep the 24-hour age buffer for temp files.
   - No registry cleaning.
   - No RAM "optimizers".
   - Locked or in-use files are skipped gracefully.
7. **Never weaken or remove a safety check to make a test pass.** Fix the code, or explain in the PR why the check is wrong.

## Tests

- `Tests/Program.cs` is a custom harness, not xUnit/NUnit:
  - every test is a method registered with `await RunTest("Area: description", TestMethod);` in `Main`;
  - a test fails by throwing via `Assert(condition, "message")`.
- **Every bug fix and feature gets a test.** For a bug, the test should fail on the old code. Security fixes test the attack itself; see the junction tests.
- Tests may only create files and folders under `Path.GetTempPath()`, and must clean them up in `finally`.
- Tests also run elevated in CI. Do not assume you are a standard user; see `SetOwnerToCurrentUser`.

## Versions and releases

- The version lives in **one** place: `Directory.Build.props`. Never hardcode a version string in code or XAML; use `AppInfo.Version` / `AppInfo.ShortVersion`.
- **Releasing = pushing a tag** (`git tag v1.2.0 && git push origin v1.2.0`). The release workflow builds, tests and publishes. Never build and upload release files by hand.
- `build-release.ps1` is the only build/packaging script, used locally, by CI and by the release workflow. Change packaging there, not in the workflow YAML.
- The installer is Inno Setup (`installer/PilgrimCleaner.iss`):
  - machine-wide only;
  - framework-dependent, needing the .NET 10 Desktop Runtime.
  Do not switch to self-contained or per-user without discussing it in an issue first.
- `winget/*.yaml` are templates; the release workflow fills in version, URL and hash. Do not edit hashes by hand.

## Code style

- Follow the existing patterns:
  - MVVM with CommunityToolkit (`[ObservableProperty]`, `[RelayCommand]`);
  - services behind interfaces in `Services/`;
  - nullable reference types enabled.
- Keep the GPL license header at the top of new `.cs` files.
- Do not swallow exceptions with empty `catch { }` in new code. Report a skip reason or log it.
- UI colors: Pilgrim Cyan `#00E5FF`, Emerald `#00FF9D`, Slate `#94A3B8`, Dark `#0C1522`.

## Git and pull requests

- Branch from `main` (`fix/…`, `feat/…`, `docs/…`, `ci/…`), one topic per branch.
- Small commits with a prefix and a body that explains **why**: `fix:`, `feat:`, `docs:`, `ci:`, `test:`, `build:`, `chore:`.
- Pull request description:
  - what changed and why;
  - how it was tested;
  - what was **not** tested.
- Never commit `bin/`, `obj/`, `dist/`, secrets, or local files such as `global.json`.
- Security problems that are not fixed yet: report them privately (see SECURITY.md), not in a public issue.

## For AI agents specifically

- Read the code you are about to change, and the tests that cover it, before editing.
- Prefer the smallest change that solves the problem. Do not reformat or rename unrelated code.
- Do not invent APIs, GitHub Actions or packages. Check that they exist; a non-existent action broke every release before.
- If a request conflicts with the safety rules above, stop and say so instead of working around them.
