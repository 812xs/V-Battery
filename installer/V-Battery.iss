; V-Battery kurulum paketi (Inno Setup 6). Önce scripts/publish.ps1 çalıştırılmalıdır.
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{6E1B7C52-3F0B-4B7A-9A5D-4C2E7B1F9D10}
AppName=V-Battery
AppVersion={#AppVersion}
AppPublisher=V-Battery
DefaultDirName={autopf}\V-Battery
DefaultGroupName=V-Battery
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=V-Battery-Setup-{#AppVersion}
SetupIconFile=..\src\VBattery.App\Assets\vbattery.ico
UninstallDisplayIcon={app}\V-Battery.exe
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
WizardStyle=modern

[Languages]
#if FileExists(CompilerPath + "Languages\Turkish.isl")
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
#endif
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek görevler:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\V-Battery.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\V-Battery"; Filename: "{app}\V-Battery.exe"
Name: "{autodesktop}\V-Battery"; Filename: "{app}\V-Battery.exe"; Tasks: desktopicon

; Varsayılan olarak Windows ile başlar; uygulama içinden (Ayarlar / tepsi menüsü) kapatılabilir.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "V-Battery"; \
  ValueData: """{app}\V-Battery.exe"" --minimized"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\V-Battery.exe"; Description: "V-Battery'yi başlat"; Flags: nowait postinstall skipifsilent
