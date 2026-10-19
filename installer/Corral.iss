; Inno Setup script for Corral.
; Build with installer\build.ps1 (it publishes the app first and passes /DAppVersion).

#define AppName "Corral"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppExe "Corral.exe"

[Setup]
; Never change AppId: it is how upgrades and uninstalls find the existing install.
AppId={{5F1B2C4E-7A3D-4E8B-9C21-6D0A8B7E3F54}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppComments=Desktop fences for Windows
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\Assets\corral.ico
OutputDir=Output
OutputBaseFilename=Corral-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Per-user install by default (no admin prompt); the wizard offers "all users" if wanted.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; We close the running app ourselves (gracefully, via --quit) rather than letting Restart Manager kill it.
CloseApplications=no
RestartApplications=no

[Tasks]
Name: "startup"; Description: "Start {#AppName} when I sign in to Windows"; GroupDescription: "Startup:"

[Files]
Source: "..\dist\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autoprograms}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

[Registry]
; Same key/value the app's own "Start with Windows" menu item uses, so the two stay in sync.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Corral"; \
    ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

[Code]
const
  RuntimeUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0';

{ ---- .NET 10 Desktop Runtime detection ---------------------------------------------------- }

function DesktopRuntimeInstalled: Boolean;
var
  Found: TFindRec;
  Base: string;
begin
  Result := False;
  Base := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\');
  if FindFirst(Base + '10.*', Found) then
  begin
    try
      repeat
        if (Found.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Result := True;
          Break;
        end;
      until not FindNext(Found);
    finally
      FindClose(Found);
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if DesktopRuntimeInstalled or WizardSilent then
    Exit;

  case MsgBox(
    '{#AppName} needs the .NET 10 Desktop Runtime (x64), which was not found on this PC.' + #13#10#13#10 +
    'Yes  - open the download page and cancel this setup (run it again after installing the runtime)' + #13#10 +
    'No   - install {#AppName} anyway (it will not start until the runtime is installed)' + #13#10 +
    'Cancel - quit setup',
    mbConfirmation, MB_YESNOCANCEL) of
    IDYES:
      begin
        ShellExec('open', RuntimeUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
        Result := False;
      end;
    IDCANCEL:
      Result := False;
  end;
end;

{ ---- stop a running copy gracefully (so it can restore the desktop icons) ------------------------ }

function CorralRunning: Boolean;
var
  Code: Integer;
begin
  Result := Exec(ExpandConstant('{cmd}'),
    '/C tasklist /FI "IMAGENAME eq {#AppExe}" | find /I "{#AppExe}"',
    '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

procedure StopCorral(const ExePath: string);
var
  Code, Tries: Integer;
begin
  if not CorralRunning then
    Exit;

  if FileExists(ExePath) then
    Exec(ExePath, '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);

  Tries := 0;
  while CorralRunning and (Tries < 20) do
  begin
    Sleep(250);
    Tries := Tries + 1;
  end;

  { Last resort so the file is not locked; the graceful path above should normally have worked. }
  if CorralRunning then
    Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopCorral(ExpandConstant('{app}\{#AppExe}'));
  Result := '';
end;

function InitializeUninstall: Boolean;
begin
  StopCorral(ExpandConstant('{app}\{#AppExe}'));
  Result := True;
end;

{ ---- uninstall: offer to remove the user's fences/settings ------------------------------------------ }

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Data: string;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  { Remove the start-with-Windows entry even if it was switched on from the app's own menu. }
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Corral');

  Data := ExpandConstant('{userappdata}\Corral');
  if DirExists(Data) and (not UninstallSilent) then
  begin
    if MsgBox('Also delete your fences and settings?' + #13#10#13#10 + Data + #13#10#13#10 +
              'Choose No to keep them in case you reinstall later.',
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(Data, True, True, True);
  end;
end;
