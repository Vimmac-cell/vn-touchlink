; VN TouchLink - Windows installer (Inno Setup 6)
; Build (from the project folder, see BUILDING.md):
;   & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\VNTouchLink.iss
; Output: build\installer\VNTouchLink-Setup.exe

#define AppName "VN TouchLink"
#define AppVersion "0.1"
#define AppPublisher "VNK Studio"
#define AppExe "VNTouchLink.exe"
#define PlayStore "https://play.google.com/store/apps/details?id=com.vnk.phonelink"
#define Website "https://github.com/Vimmac-cell/vn-touchlink"

[Setup]
AppId={{86050FA9-B4CC-41C8-B3CE-26BFD7BD2C78}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#Website}
AppSupportURL={#Website}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\build\installer
OutputBaseFilename=VNTouchLink-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=yes

[Tasks]
Name: "startup"; Description: "Start {#AppName} when Windows starts"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "..\build\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\publish\adb\*"; DestDir: "{app}\adb"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Get the phone app (Play Store)"; Filename: "{#PlayStore}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent
Filename: "{#PlayStore}"; Description: "Open the Play Store page to get the phone app"; Flags: shellexec postinstall skipifsilent unchecked

[UninstallRun]
; Stop our adb copy so its files can be removed
Filename: "{app}\adb\adb.exe"; Parameters: "kill-server"; Flags: runhidden; RunOnceId: "KillAdb"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\adb"
