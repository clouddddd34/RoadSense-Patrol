#define AppName "RoadSense Patrol"
#define AppVersion "0.7.2"
#ifndef GamePublishDir
#define GamePublishDir "..\publish"
#endif

[Setup]
AppId={{D48A4647-C28D-4D97-954C-0276ECF908DF}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=RoadSense
AppPublisherURL=https://github.com/clouddddd34/RoadSense-Patrol
AppSupportURL=https://github.com/clouddddd34/RoadSense-Patrol/issues
DefaultDirName={localappdata}\Programs\RoadSense Patrol
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=RoadSensePatrol-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\RoadSensePatrol.exe
SetupLogging=yes
SetupIconFile=..\assets\roadsense-app.ico
CloseApplications=yes
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#GamePublishDir}\RoadSensePatrol.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#GamePublishDir}\settings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\RoadSense Patrol"; Filename: "{app}\RoadSensePatrol.exe"
Name: "{autodesktop}\RoadSense Patrol"; Filename: "{app}\RoadSensePatrol.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RoadSensePatrol.exe"; Description: "Launch RoadSense Patrol"; Flags: nowait postinstall skipifsilent
