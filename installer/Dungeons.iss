; Inno Setup script for the Windows installer: Temnorid-Dungeons-Setup.exe
; Build the player first (Unity: Temnorid > Dungeons > Build Windows Player), then compile this with Inno Setup:
;   "ISCC.exe" installer\Dungeons.iss
; The installer is written to Builds\Installer.

#define AppName "Temnorid Dungeons"
#define AppVersion "0.2.0"
#define AppExe "TemnoridDungeons.exe"

[Setup]
AppId={{9D1E5B72-3A6C-4F08-B2D4-6E8A0C7F1B33}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Temnorid
AppPublisherURL=https://dungeons.temnorid.com
DefaultDirName={autopf}\Temnorid Dungeons
DefaultGroupName=Temnorid Dungeons
DisableProgramGroupPage=yes
OutputDir=..\Builds\Installer
OutputBaseFilename=Temnorid-Dungeons-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\Builds\Windows\TemnoridDungeons\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; No skipifsilent: when the game updates itself the installer runs silently and must start the game again.
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall
