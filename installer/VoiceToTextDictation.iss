#define MyAppName "Dictée universelle"
#define MyAppExeName "VoiceToText.exe"
#define MyAppPublisher "MafLabs38"
#define MyAppURL "https://github.com/MafLabs38/voice-to-text-dev"
#define MyPublishDir "..\src\VoiceToText\bin\Release\net10.0-windows\win-x64\publish"

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

[Setup]
; Identifiant fixe : ne jamais changer, sinon Windows ne verra plus les futures versions
; comme une mise à jour de la même appli (raccourcis dupliqués, désinstallation cassée).
AppId={{6C6C2B6F-8C0B-4B9E-9F5C-6B7A6F5E4D3C}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\VoiceToTextDictation
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Installation par utilisateur courant, sans droits admin (pas de UAC, pas d'écriture hors profil).
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=VoiceToTextDictation-Setup-{#MyAppVersion}
SetupIconFile=..\src\VoiceToText\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent
