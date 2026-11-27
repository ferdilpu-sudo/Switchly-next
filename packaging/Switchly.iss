; Switchly — Inno Setup 6 Per-User Installer Script (Windows 11 x64)
; Aligned with ADR-008 and Product Owner Decision D8 (2026-11-25).
; Compile via: ISCC.exe /DAppVersion=0.9.0-beta.1 /DPublishDir=..\artifacts\publish\win-x64 packaging\Switchly.iss

#ifndef AppVersion
  #define AppVersion "0.9.0-beta.1"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={{6D2B74E4-54B3-4F26-9B90-7C1D42F58C11}
AppName=Switchly
AppVersion={#AppVersion}
AppVerName=Switchly {#AppVersion}
AppPublisher=Switchly
AppPublisherURL=https://www.switchly.site/
AppSupportURL=https://www.switchly.site/
AppUpdatesURL=https://www.switchly.site/
DefaultDirName={localappdata}\Programs\Switchly
DefaultGroupName=Switchly
DisableProgramGroupPage=yes
InfoBeforeFile=..\docs\PRIVACY.md
; Per-user install avoids UAC elevation and preserves CEF non-elevated runtime policy (exit code 38).
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 11 x64 minimum (build 22000) per D8 / OPEN-002.
MinVersion=10.0.22000
OutputDir={#OutputDir}
OutputBaseFilename=Switchly-{#AppVersion}-win-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Switchly.App\Assets\switchly.ico
CloseApplications=force
CloseApplicationsFilter=*.exe,*.dll
UninstallDisplayIcon={app}\Switchly.App.exe
; For public GA release with an Authenticode certificate, enable SignTool in Inno Setup IDE or CLI:
; SignTool=signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Buat pintasan di Desktop"; GroupDescription: "Pintasan tambahan:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Switchly"; Filename: "{app}\Switchly.App.exe"
Name: "{autodesktop}\Switchly"; Filename: "{app}\Switchly.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Switchly.App.exe"; Description: "Jalankan Switchly sekarang"; Flags: nowait postinstall skipifsilent

; Note: User profiles and local database live under %LOCALAPPDATA%\SwitchlyNext and are intentionally
; preserved on uninstall so upgrades/reinstalls never destroy user sessions or encrypted vault data.
