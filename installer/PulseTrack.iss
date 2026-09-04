; PulseTrack.Taskbar installer (Inno Setup 6).
; Build order:
;   1. dotnet publish PulseTrack.Taskbar.csproj -c Release -r win-x64 -o publish-portable /p:PublishSingleFile=true --self-contained "/p:IncludeNativeLibrariesForSelfExtract=true" /p:EnableCompressionInSingleFile=true /p:PublishReadyToRun=true
;   2. Compile this script with ISCC.exe (Inno Setup 6, Unicode).
; Output: dist/PulseTrack-Setup-<version>.exe

#define MyAppName "PulseTrack"
#define MyAppExe "PulseTrack.Taskbar.exe"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "PulseTrack"
#define MyAppURL "https://github.com/"
#define SourceDir "publish-portable"

[Setup]
AppId={{8F3A2C41-7B1D-4E9A-A5C3-2F6D1B9E4A01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=dist
OutputBaseFilename=PulseTrack-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Start with Windows"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
; PDB intentionally excluded: debug symbols ship nothing for end users.

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PulseTrackTaskbar"; ValueData: """{app}\{#MyAppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
