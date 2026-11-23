; Inno Setup script for FPM (Windows PC Manager)
; Canonical AppId preserved for seamless in-place upgrade.

#define MyAppName "FPM"
#define MyAppVersion "3.23.3"
#define MyAppPublisher "FPM"
#define MyAppURL "https://github.com/farmanullah1/FPM"
#define MyAppExeName "FPM.exe"
#define MyAppHelperExeName "FPM.Helper.exe"

[Setup]
AppId={{8A4F2C1E-74B2-4E8F-96E8-9F2B0F1C3A5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\publish\installer
OutputBaseFilename=FPM_Setup_{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startuprun"; Description: "Start FPM when Windows starts"; GroupDescription: "Windows Integration:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FPM"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startuprun
Root: HKCU; Subkey: "Software\FPM"; Flags: uninsdeletekeyifempty

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\FPM\logs"
Type: filesandordirs; Name: "{localappdata}\FPM\backups"
