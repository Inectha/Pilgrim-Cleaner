; Pilgrim Cleaner - Inno Setup Script
; Windows Disk Cleaner, App Manager & Hardware Health
; Copyright (C) 2026 Pilgrim Software
;
; Built by the release workflow:   ISCC /DMyAppVersion=1.2.0 installer\PilgrimCleaner.iss
; Expects the published app (GUI + CLI) in ..\dist\PilgrimCleaner (see build-release.ps1).
;
; Design choices:
; - Machine-wide only (C:\Program Files). Pilgrim Cleaner runs elevated and its CLI as SYSTEM;
;   a per-user install folder would let a standard user replace files that later run as admin.
; - Framework-dependent: needs the .NET 10 Desktop Runtime, which Windows/Microsoft Update keeps
;   patched independently of Pilgrim Cleaner releases.
; - Silent install for RMM/Intune/winget:  PilgrimCleanerSetup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART

#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif
#define MyAppName "Pilgrim Cleaner"
#define MyAppPublisher "Pilgrim Software"
#define MyAppURL "https://github.com/Inectha/Pilgrim-Cleaner"
#define MyAppExeName "PilgrimCleaner.exe"
#define DotNetMajor "10"

[Setup]
AppId={{8B41D86E-A52A-4D78-99A0-318D740E7DF1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\Pilgrim Cleaner
DefaultGroupName=Pilgrim Cleaner
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist\installer
OutputBaseFilename=PilgrimCleanerSetup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ChangesEnvironment=yes
CloseApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Pilgrim Cleaner Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "addtopath"; Description: "Add the 'pilgrim' command-line tool to the system PATH"; GroupDescription: "Command line:"

[InstallDelete]
; Left behind by the old PilgrimCleanerSetup.exe / Install-PilgrimCleaner.ps1 installers (v1.0 - v1.1).
Type: files; Name: "{app}\Uninstall.bat"

[Files]
Source: "..\dist\PilgrimCleaner\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Remove the uninstall entry of the old custom installer, so "Installed apps" shows Pilgrim Cleaner once.
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\PilgrimCleaner"; Flags: deletekey
; Add {app} to the machine PATH (removed again on uninstall, see [Code]).
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Tasks: addtopath; Check: NeedsAddPath(ExpandConstant('{app}'))

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Remove the background maintenance task that the app can register (and its pre-1.0 name).
Filename: "{sys}\schtasks.exe"; Parameters: "/delete /tn ""PilgrimCleanerAutoMaintenance"" /f"; Flags: runhidden; RunOnceId: "DelTask"
Filename: "{sys}\schtasks.exe"; Parameters: "/delete /tn ""LuminaSuiteAutoMaintenance"" /f"; Flags: runhidden; RunOnceId: "DelLegacyTask"

[Code]
const
  EnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/{#DotNetMajor}.0';

function IsDesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  { The .NET Desktop Runtime installs one folder per version, e.g. ...\Microsoft.WindowsDesktop.App\10.0.1 }
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\{#DotNetMajor}.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDesktopRuntimeInstalled then
  begin
    if SuppressibleMsgBox('Pilgrim Cleaner needs the .NET {#DotNetMajor} Desktop Runtime (x64), which is not installed.' + #13#10#13#10 +
                          'Open the download page now? Run this setup again after installing the runtime.',
                          mbConfirmation, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;

function PathContains(const Paths, Dir: string): Boolean;
begin
  Result := Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(Paths) + ';') > 0;
end;

function NeedsAddPath(Dir: string): Boolean;
var
  Paths: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Paths) then
    Paths := '';
  Result := not PathContains(Paths, Dir);
end;

procedure RemoveFromPath(Dir: string);
var
  Paths: string;
  P: Integer;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Paths) then
    Exit;
  Paths := ';' + Paths + ';';
  P := Pos(';' + Uppercase(Dir) + ';', Uppercase(Paths));
  if P = 0 then
    Exit;
  Delete(Paths, P, Length(Dir) + 1);
  { Strip the helper semicolons added above }
  Paths := Copy(Paths, 2, Length(Paths) - 2);
  RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Paths);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveFromPath(ExpandConstant('{app}'));
end;
