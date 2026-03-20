[Setup]
AppId={{8E17A3F2-8C6A-4CBA-8D6A-0E0D2E53F4A1}
AppName=DatabaseStarter
AppVersion=0.1.2
AppPublisher=DatabaseStarter
DefaultDirName={localappdata}\DatabaseStarter\App
DefaultGroupName=DatabaseStarter
DisableProgramGroupPage=yes
OutputBaseFilename=DatabaseStarter_Setup_0.1.2
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
SetupIconFile=DatabaseStarter.ico
UninstallDisplayIcon={app}\DatabaseStarter.exe
CreateUninstallRegKey=yes
Uninstallable=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktopverknuepfung erstellen"; GroupDescription: "Zusaetzliche Verknuepfungen:"; Flags: unchecked
Name: "startmenuicon"; Description: "Startmenue-Eintrag erstellen"; GroupDescription: "Zusaetzliche Verknuepfungen:"; Flags: unchecked

[Files]
Source: ".\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autodesktop}\DatabaseStarter"; Filename: "{app}\DatabaseStarter.exe"; Tasks: desktopicon
Name: "{autoprograms}\DatabaseStarter\DatabaseStarter"; Filename: "{app}\DatabaseStarter.exe"; Tasks: startmenuicon
Name: "{autoprograms}\DatabaseStarter\Deinstallieren DatabaseStarter"; Filename: "{uninstallexe}"; Tasks: startmenuicon

[Run]
Filename: "{app}\DatabaseStarter.exe"; Description: "DatabaseStarter starten"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
