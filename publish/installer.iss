; ══════════════════════════════════════════════════════
;  SecureVault — Inno Setup Installer Script
;  Bu fayl to'g'ridan-to'g'ri ishlatilmaydi.
;  publish.ps1 orqali chaqiriladi.
; ══════════════════════════════════════════════════════

#define AppName      "SecureVault"
#define AppVersion   "1.0.0"
#define AppPublisher "SecureVault"
#define AppExeName   "PasswordManager.exe"

; AppSourceDir — publish.ps1 tomonidan beriladi: /DAppSourceDir="to'liq yo'l"
#ifndef AppSourceDir
  #error Bu faylni to'g'ridan-to'g'ri ishlatmang. publish.ps1 orqali chaqiring.
#endif

[Setup]
AppId={{F3A7C2B1-D094-4E8F-BC23-1A2B3C4D5E6F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=.
OutputBaseFilename=SecureVault_Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";   Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
