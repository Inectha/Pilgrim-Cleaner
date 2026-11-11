# Installation & Enterprise Deployment Guide — Pilgrim Cleaner

Pilgrim Cleaner supports interactive installation, portable execution, and unattended enterprise / Windows Server deployment.

**Requirement:** the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0). Windows Update / Microsoft Update keeps it patched. Winget installs it automatically; the setup wizard checks for it and links to the download if it is missing.

---

## Option 1: Setup Wizard (`PilgrimCleanerSetup.exe`)

1. Download **`PilgrimCleanerSetup.exe`** from [GitHub Releases](https://github.com/Inectha/Pilgrim-Cleaner/releases).
2. Run it. The installer asks for administrator rights and installs machine-wide to `C:\Program Files\Pilgrim Cleaner`. That location satisfies default AppLocker and WDAC path rules, and a standard user cannot change files there that later run as administrator or SYSTEM.
3. Optionally, it adds the `pilgrim` CLI to the system `PATH` (on by default).

**Unattended / RMM / Intune:**
```powershell
.\PilgrimCleanerSetup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```
Add `/TASKS="desktopicon,addtopath"` to choose the optional tasks explicitly, and `/LOG="C:\Temp\pilgrim-setup.log"` for a log file.

---

## Option 2: Winget

```powershell
winget install PilgrimSoftware.PilgrimCleaner
```

Winget installs the .NET 10 Desktop Runtime dependency first. Each GitHub Release has the matching manifests attached as the `winget-manifests-<version>` workflow artifact, so you can also test locally:
```powershell
winget install --manifest .\winget-manifests-1.2.0\
```

---

## Option 3: Portable ZIP (no installation)

1. Download **`PilgrimCleaner-v<version>-Windows-x64.zip`** (GUI + CLI).
2. Extract it to a folder that standard users cannot write to if you will run it elevated (e.g. `C:\Tools\PilgrimCleaner` with default ACLs is **not** suitable, `C:\Program Files\...` is).
3. Run `PilgrimCleaner.exe` for the desktop UI, or `.\pilgrim.exe man` for the CLI manual.

Every release includes `SHA256SUMS.txt` to verify the downloads:
```powershell
Get-FileHash .\PilgrimCleanerSetup.exe -Algorithm SHA256
```

---

## How to Uninstall

- **Windows Settings:** go to **Settings → Apps → Installed apps**, find **Pilgrim Cleaner** and click **Uninstall**. This also removes the `PATH` entry and the background maintenance task.
- **Unattended:**
  ```powershell
  & "C:\Program Files\Pilgrim Cleaner\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  ```

### Upgrading from v1.0 / v1.1
Installing over a machine-wide v1.0/v1.1 install replaces it and removes its old uninstall entry. A **per-user** v1.0/v1.1 install (in `%LOCALAPPDATA%\Programs\Pilgrim Cleaner`) is not touched: uninstall it first from **Installed apps**.
