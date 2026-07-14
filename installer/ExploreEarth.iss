#define MyAppName "ExploreEarth"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ExploreEarth contributors"
#define MyAppExeName "ExploreEarth.exe"

[Setup]
AppId={{7F8E51D7-8B54-4D66-BB4B-F5A9F7E3AE21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/saveliyshap22-crypto/ExploreEarth
AppSupportURL=https://github.com/saveliyshap22-crypto/ExploreEarth/issues
DefaultDirName={autopf}\ExploreEarth
DefaultGroupName=ExploreEarth
AllowNoIcons=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
OutputDir=..\artifacts\downloads
OutputBaseFilename=ExploreEarth-{#MyAppVersion}-Setup-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupMutex=ExploreEarthSetupMutex
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные ярлыки:"; Flags: unchecked

[Files]
Source: "..\artifacts\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\prerequisites\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\artifacts\prerequisites\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\ExploreEarth"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\ExploreEarth"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Установка Microsoft Visual C++ Runtime…"; Flags: waituntilterminated runhidden
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Установка Microsoft Edge WebView2 Runtime…"; Flags: waituntilterminated runhidden; Check: not IsWebView2Installed
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить ExploreEarth"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  WebView2ClientId = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function IsUsableVersion(const Version: String): Boolean;
begin
  Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function IsWebView2Installed: Boolean;
var
  Version: String;
begin
  Result := False;

  if RegQueryStringValue(HKLM32,
       'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2ClientId,
       'pv', Version) and IsUsableVersion(Version) then
  begin
    Result := True;
    Exit;
  end;

  if RegQueryStringValue(HKCU,
       'Software\Microsoft\EdgeUpdate\Clients\' + WebView2ClientId,
       'pv', Version) and IsUsableVersion(Version) then
  begin
    Result := True;
  end;
end;
