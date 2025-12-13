; Synktra Companion Installer Script
; Requires Inno Setup 6.0 or later (https://jrsoftware.org/isinfo.php)

#define MyAppName "Synktra Companion"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Scriptaxy"
#define MyAppURL "https://github.com/scriptaxy/gaming-hub"
#define MyAppExeName "SynktraCompanion.exe"

[Setup]
; App identification
AppId={{8E9F4A2B-5C3D-4E6F-9A1B-2C3D4E5F6A7B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases

; Installation settings
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=yes

; UPGRADE SUPPORT - Use previous install location if upgrading
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes

; Output settings
OutputDir=Output
OutputBaseFilename=SynktraCompanion_Setup_{#MyAppVersion}
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Privileges (needed for ViGEmBus installation)
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Modern look
WizardStyle=modern
WizardSizePercent=100

; Minimum Windows version (Windows 10)
MinVersion=10.0

; License and info
LicenseFile=License.txt
InfoBeforeFile=ReadMe.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode
Name: "startupentry"; Description: "Start Synktra Companion when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked
Name: "installvigem"; Description: "Install ViGEmBus driver (required for virtual controller support)"; GroupDescription: "Virtual Controller:"; Flags: checkedonce
Name: "firewall"; Description: "Add Windows Firewall rules (required for remote connections)"; GroupDescription: "Network:"; Flags: checkedonce

[Files]
; Main application files (from publish output)
Source: "..\bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ViGEmBus installer (downloaded during build or included)
Source: "Dependencies\ViGEmBus_Setup.exe"; DestDir: "{tmp}"; Flags: ignoreversion deleteafterinstall; Tasks: installvigem

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Registry]
; Startup entry
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SynktraCompanion"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startupentry

[Run]
; Install ViGEmBus driver
Filename: "{tmp}\ViGEmBus_Setup.exe"; Parameters: "/passive /norestart"; StatusMsg: "Installing ViGEmBus driver..."; Flags: waituntilterminated; Tasks: installvigem

; Add firewall rules
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""Synktra Companion API"" dir=in action=allow protocol=tcp localport=19500"; StatusMsg: "Adding firewall rules..."; Flags: runhidden waituntilterminated; Tasks: firewall
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""Synktra Companion Stream WS"" dir=in action=allow protocol=tcp localport=19501"; Flags: runhidden waituntilterminated; Tasks: firewall
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""Synktra Companion Stream UDP"" dir=in action=allow protocol=udp localport=19502"; Flags: runhidden waituntilterminated; Tasks: firewall
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""Synktra Companion Audio"" dir=in action=allow protocol=tcp localport=19503"; Flags: runhidden waituntilterminated; Tasks: firewall
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""Synktra Companion Discovery"" dir=in action=allow protocol=udp localport=5001"; Flags: runhidden waituntilterminated; Tasks: firewall

; Launch after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove firewall rules on uninstall
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""Synktra Companion API"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""Synktra Companion Stream WS"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""Synktra Companion Stream UDP"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""Synktra Companion Audio"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""Synktra Companion Discovery"""; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{localappdata}\SynktraCompanion"

[Code]
var
  ViGEmDownloadPage: TDownloadWizardPage;
  ExistingVersion: String;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := True;
end;

function GetExistingVersion: String;
var
  Version: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8E9F4A2B-5C3D-4E6F-9A1B-2C3D4E5F6A7B}_is1', 'DisplayVersion', Version) then
    Result := Version
  else if RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8E9F4A2B-5C3D-4E6F-9A1B-2C3D4E5F6A7B}_is1', 'DisplayVersion', Version) then
    Result := Version;
end;

function InitializeSetup: Boolean;
var
  Msg: String;
begin
  Result := True;
  ExistingVersion := GetExistingVersion;
  
  if ExistingVersion <> '' then
  begin
    if ExistingVersion = '{#MyAppVersion}' then
    begin
    Msg := 'Synktra Companion version ' + ExistingVersion + ' is already installed.' + #13#10 + #13#10 +
             'Do you want to repair/reinstall?';
      Result := MsgBox(Msg, mbConfirmation, MB_YESNO) = IDYES;
    end
    else
    begin
      Msg := 'Synktra Companion version ' + ExistingVersion + ' is currently installed.' + #13#10 + #13#10 +
      'This will upgrade to version {#MyAppVersion}.' + #13#10 + #13#10 +
 'Continue with upgrade?';
      Result := MsgBox(Msg, mbConfirmation, MB_YESNO) = IDYES;
    end;
  end;
end;

procedure InitializeWizard;
begin
  ViGEmDownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
  
  // Show what's happening in the wizard
  if ExistingVersion <> '' then
  begin
    if ExistingVersion = '{#MyAppVersion}' then
      WizardForm.PageNameLabel.Caption := 'Repair/Reinstall'
    else
      WizardForm.PageNameLabel.Caption := 'Upgrade from ' + ExistingVersion + ' to {#MyAppVersion}';
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  
  if CurPageID = wpReady then
  begin
    // Check if ViGEmBus installer exists, if not download it
    if WizardIsTaskSelected('installvigem') then
    begin
      if not FileExists(ExpandConstant('{tmp}\ViGEmBus_Setup.exe')) and 
       not FileExists(ExpandConstant('{src}\Dependencies\ViGEmBus_Setup.exe')) then
      begin
        ViGEmDownloadPage.Clear;
   ViGEmDownloadPage.Add('https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe', 'ViGEmBus_Setup.exe', '');
        ViGEmDownloadPage.Show;
   try
      try
            ViGEmDownloadPage.Download;
   Result := True;
          except
      if ViGEmDownloadPage.AbortedByUser then
  Log('Download aborted by user.')
        else
      SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
 Result := False;
          end;
    finally
   ViGEmDownloadPage.Hide;
        end;
      end;
    end;
  end;
end;

function IsViGEmInstalled: Boolean;
var
  ResultCode: Integer;
begin
  // Check if ViGEmBus service exists
  Result := Exec('sc', 'query ViGEmBus', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
if CurStep = ssPostInstall then
  begin
  // Check if ViGEmBus was installed successfully
    if WizardIsTaskSelected('installvigem') then
    begin
      if IsViGEmInstalled then
        Log('ViGEmBus driver installed successfully')
      else
        Log('ViGEmBus driver installation may have failed or requires restart');
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  NeedsRestart := False;
end;

// Check if .NET 9 runtime is installed
function IsDotNet9Installed: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  // Note: For self-contained publish, .NET runtime is bundled, so this check is optional
end;
