; ServDyne POS - Inno Setup installer script
; 1. Publish:   dotnet publish src\Afsara.WinForms -p:PublishProfile=ServDynePOS-win-x64
; 2. Compile:   right-click this file -> Compile  (requires Inno Setup 6+)
; Output:       installer\output\ServDynePOS_Setup_1.0.0.exe

#define MyAppName "ServDyne POS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ServDyne"
#define MyAppExeName "ServDynePOS.exe"

[Setup]
AppId={{8B3E4A1F-7C2D-4E5A-9B1C-3F6D8E2A5C41}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=ServDynePOS_Setup_{#MyAppVersion}
SetupIconFile=..\src\Afsara.WinForms\Assets\servdyne.ico
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Everything produced by 'dotnet publish' (single-file exe + any extracted natives)
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "appsettings.json,*.pdb"
; Connection string config - installed once, never overwritten on upgrade
Source: "publish\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
