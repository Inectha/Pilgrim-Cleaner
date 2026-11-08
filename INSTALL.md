# Installation & Deployment Guide — Pilgrim Cleaner

Pilgrim Cleaner offers three simple installation options for Windows 10 and Windows 11.

---

## Option 1: 1-Click Native Installer (Recommended)

Included directly with the download archive or repository:

1. Download or extract the release package.
2. Double-click **`Install.bat`** (or right-click `Install-LuminaSuite.ps1` $\rightarrow$ *Run with PowerShell*).
3. **What the installer does automatically:**
   - Copies application binaries to `%LOCALAPPDATA%\Programs\Pilgrim Cleaner`.
   - Creates a **Desktop Shortcut** with high-resolution vector icon.
   - Creates a **Start Menu Shortcut** in `Pilgrim Cleaner`.
   - Registers Pilgrim Cleaner in **Windows Settings $\rightarrow$ Installed Apps** (*Add or Remove Programs*).
   - Generates a 1-click uninstaller `Uninstall.bat`.
   - Launches Pilgrim Cleaner upon completion.

---

## Option 2: Portable Mode (Zero Installation)

If you prefer to run Pilgrim Cleaner without modifying your user profile or from a USB flash drive:

1. Download **`PilgrimCleaner-v1.0.0-Windows-x64.zip`**.
2. Extract the archive to any folder of your choice.
3. Double-click **`PilgrimCleaner.exe`** to launch immediately.
4. All configuration is self-contained. Deleting the folder removes the application completely.

---

## Option 3: Setup Wizard (`PilgrimCleanerSetup.exe`)

For standard Windows Setup Wizard installation:

1. Download the compiled installer **`PilgrimCleanerSetup.exe`** from [GitHub Releases](https://github.com/Inectha/Pilgrim-Cleaner/releases).
2. Double-click the `.exe` to launch the setup wizard.
3. Choose your destination folder.
4. Select optional tasks (Create Desktop Shortcut, Start Menu Shortcut).
5. Click **Install**.

---

## How to Uninstall

Pilgrim Cleaner provides clean, complete removal without leaving orphaned files or registry entries:

- **Via Windows Settings:**  
  Go to **Windows Settings $\rightarrow$ Apps $\rightarrow$ Installed Apps**, find **Pilgrim Cleaner**, and click **Uninstall**.
- **Via Start Menu:**  
  Start Menu $\rightarrow$ Pilgrim Cleaner $\rightarrow$ **Uninstall Pilgrim Cleaner**.
- **Via Install Folder:**  
  Navigate to `%LOCALAPPDATA%\Programs\Pilgrim Cleaner` and double-click **`Uninstall.bat`**.
