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
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022, JetBrains Rider, or VS Code / Antigravity

### Build & Run
```powershell
# Restore dependencies and build
dotnet build

# Launch Pilgrim Cleaner
dotnet run
```

### Running Tests
Before opening a pull request, ensure all test suites pass with zero failures:
```powershell
# 1. Run core architecture test suite (21 tests)
dotnet run --project Tests/PilgrimCleaner.Tests.csproj

# 2. Run end-user audit harness (11 feature modules)
dotnet run --project Tests/PilgrimCleaner.Tests.csproj -- --user-audit
```

---

## Pull Request Guidelines

1. **Fork the Repository** and create your branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. **Commit Messages:** Follow concise, descriptive commit messages (e.g., `feat: add rust cargo cache cleaner`, `fix: handle edge case in startup enumerator`).
3. **Keep UI Cohesive:** Follow the Pilgrim design system (Pilgrim Cyan `#00E5FF`, Radiant Emerald `#00FF9D`, Slate `#94A3B8`, Dark `#0C1522`).
4. **Submit PR:** Open a Pull Request against `main` with a clear description of changes.
