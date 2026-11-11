# Contributing to Pilgrim Cleaner

Thank you for your interest in contributing to **Pilgrim Cleaner**!

---

## Safety Principles

All contributions must strictly uphold Pilgrim Cleaner's core safety commitments:

1. **Zero Registry Tampering:** PRs that introduce registry cleaning, registry pruning, or scanning of the Windows Registry for "invalid keys" will be rejected.
2. **Zero RAM Hacks:** No artificial working set dumps or `EmptyWorkingSet` memory flush routines.
3. **24-Hour Age Safety Buffer:** Temporary file cleaners must maintain a minimum 24-hour age buffer before targeting files for deletion.
4. **Resilient File Handling:** All file I/O must implement robust error handling that gracefully skips locked or in-use files.
5. **Driver Safety:** Driver queries must interact exclusively with native Microsoft Windows Update Sessions (`WUApiLib`) or WHQL certified catalogs.

---

## Development Setup

### Prerequisites
- Windows 10 (1903+) or Windows 11
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022, JetBrains Rider, or VS Code / Antigravity

### Build & Run
```powershell
# Restore dependencies and build
dotnet build

# Launch Pilgrim Cleaner
dotnet run
```

### Running Tests
Before opening a pull request, ensure the architecture test suite passes with zero failures. The CI workflow runs the same suite on every pull request.
```powershell
dotnet run --project Tests/PilgrimCleaner.Tests.csproj
```

The end-user audit harness changes real system state: it registers a scheduled task and toggles a startup entry. Run it only in a VM or Windows Sandbox (the release workflow runs it on a throwaway GitHub runner):
```powershell
dotnet run --project Tests/PilgrimCleaner.Tests.csproj -- --user-audit
```

---

## Releasing a New Version

Releases are built by GitHub Actions (`.github/workflows/build-and-release.yml`). You do not build or upload anything by hand.

1. Make sure `main` is green (CI passes) and update `CHANGELOG.md`.
2. Tag the release and push the tag:
   ```powershell
   git tag v1.2.0
   git push origin v1.2.0
   ```
3. The workflow tests, builds and publishes a GitHub Release with `PilgrimCleanerSetup.exe`, the portable ZIP and `SHA256SUMS.txt`. The version comes from the tag; `Directory.Build.props` only sets the version for local builds.
4. Download the `winget-manifests-1.2.0` artifact from the workflow run and submit it to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) (for example with `wingetcreate submit`).

To test the packaging locally, run `.\build-release.ps1 -Version 1.2.0`. The installer step needs [Inno Setup 6.3 or newer](https://jrsoftware.org/isinfo.php).

---

## Pull Request Guidelines

1. **Fork the Repository** and create your branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. **Commit Messages:** Follow concise, descriptive commit messages (e.g., `feat: add rust cargo cache cleaner`, `fix: handle edge case in startup enumerator`).
3. **Keep UI Cohesive:** Follow the Pilgrim design system (Pilgrim Cyan `#00E5FF`, Radiant Emerald `#00FF9D`, Slate `#94A3B8`, Dark `#0C1522`).
4. **Submit PR:** Open a Pull Request against `main` with a clear description of changes.
