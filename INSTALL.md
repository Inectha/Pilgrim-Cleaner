# Installation & Enterprise Deployment Guide — Pilgrim Cleaner v1.1.0

Pilgrim Cleaner supports interactive workstation installation, portable execution, and unattended enterprise/Windows Server deployment.

---

## Option 1: Setup Wizard & Unattended Deployment (`PilgrimCleanerSetup.exe`)

1. Download **`PilgrimCleanerSetup.exe`** from [GitHub Releases](https://github.com/Inectha/Pilgrim-Cleaner/releases).
2. **Interactive Mode:** Double-click `PilgrimCleanerSetup.exe`.
   - When launched as **Administrator**, the installer automatically targets `C:\Program Files\Pilgrim Cleaner` (compliant with default AppLocker and WDAC policies) and registers the `pilgrim` CLI in the Machine `PATH`.
   - When launched as a **Standard User**, the installer targets `%LOCALAPPDATA%\Programs\Pilgrim Cleaner` and registers `pilgrim` in the User `PATH`.
3. **Unattended / RMM / Intune Silent Mode:**
   ```powershell
   .\PilgrimCleanerSetup.exe /S
   ```

---

## Option 2: 1-Click PowerShell / Batch Installer

Included inside the release archive:

- **Standard User:** Double-click **`Install.bat`**.
- **Windows Server / Machine-Wide Admin Install:**
  ```powershell
  .\Install-PilgrimCleaner.ps1 -Machine -Silent -NoLaunch
  ```
  This deploys both `PilgrimCleaner.exe` (GUI) and `pilgrim.exe` (Sysadmin CLI) to `C:\Program Files\Pilgrim Cleaner`, registers the uninstaller in `HKLM`, and adds `pilgrim.exe` to the system `PATH`.

---

## Option 3: Portable ZIP Archive (Zero Installation)

1. Download **`PilgrimCleaner-v1.1.0-Windows-x64.zip`** (full GUI + CLI) or **`PilgrimCleaner-CLI-v1.1.0-Windows-x64.zip`** (headless Windows Server edition).
2. Extract to any directory (e.g. `C:\Tools\PilgrimCleaner`).
3. Run `PilgrimCleaner.exe` for the desktop UI or `.\pilgrim.exe man` in PowerShell for the CLI manual.

---

## Option 4: Winget Package Manager

Once published to the Windows Package Manager catalog (or using the local manifest in `winget/`):

```powershell
# Install from official manifest or local winget directory
winget install --manifest .\winget\ --scope machine
```

---

## How to Uninstall

Pilgrim Cleaner provides complete removal without leaving orphaned files or registry entries:

- **Via Windows Settings:**  
  Go to **Windows Settings $\rightarrow$ Apps $\rightarrow$ Installed Apps**, find **Pilgrim Cleaner**, and click **Uninstall**.
- **Unattended CLI Uninstall:**
  ```powershell
  "C:\Program Files\Pilgrim Cleaner\Uninstall.bat" --silent
  ```
