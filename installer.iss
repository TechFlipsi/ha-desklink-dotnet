; HA DeskLink Inno Setup Installer
#define AppName "HA DeskLink"
#define AppExe "HA_DeskLink.exe"

[Setup]
AppName={#AppName}
AppVersion=2.0.5
AppPublisher=Fabian Kirchweger
AppPublisherURL=https://github.com/FKirchweger/ha-desklink-dotnet
AppSupportURL=https://github.com/FKirchweger/ha-desklink-dotnet/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=output
OutputBaseFilename=HA_DeskLink_Setup_2.0.5
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=force
RestartApplications=no
SetupIconFile=src\HaDeskLink\Assets\icon.ico

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{#AppName} starten"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"