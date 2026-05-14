; Modena Model Health Checker Multi-Version Installer Script
; Supports Revit 2024-2026
; Created: 2026-03-17
;
; This script:
; 1. Detects installed Revit versions (2024-2026)
; 2. Deploys Model Health Checker to each detected Revit version
; 3. Creates .addin manifest files for each version
; 4. Deploys modena-config.json with obfuscated APS credentials
; 5. Supports clean uninstallation
; 6. Integrates with Windows Programs & Features
;
; PREREQUISITE: Run build-multi-version.ps1 before compiling this script.

; --- APS credentials (defaults to production; optional override with /D) ---
#ifndef ApsClientId
  #define ApsClientId "GKshdbezSremlBFGrg58fkePbadhIXAcW1MM0MgclDxct2Aq"
#endif
#ifndef ApsClientSecret
  #define ApsClientSecret "FGZKDG6oN9qN0Oj0HOICWvad0GusD2O3tGxgdW4kAdS3BKa2mrxRYiQVhJFMIkwi"
#endif
; Build: simply Compile (F9) in Inno Setup.
; To override for testing: add /DApsClientId=... /DApsClientSecret=... in "Compile with Parameters".

; Optional compile-time fingerprint (off by default). Define /DSHOW_APS_FINGERPRINT=1 to print safe fingerprints.
#define _APS_ID ApsClientId
#define _APS_SEC ApsClientSecret
#ifdef SHOW_APS_FINGERPRINT
  #define _ID_F4  Copy(_APS_ID, 1, 4)
  #define _ID_L4  Copy(_APS_ID, Len(_APS_ID)-3, 4)
  #define _SEC_F4 Copy(_APS_SEC, 1, 4)
  #define _SEC_L4 Copy(_APS_SEC, Len(_APS_SEC)-3, 4)
  #pragma message "APS Client ID fingerprint: " + _ID_F4 + "..." + _ID_L4
  #pragma message "APS Client Secret fingerprint: " + _SEC_F4 + "..." + _SEC_L4
#endif

#define AppVersion "1.0.3"

[Setup]
AppId={{B2C3D4E5-F6A7-4B8C-9D0E-1F2A3B4C5D6E}}
AppName=Modena Model Health Checker
AppVersion={#AppVersion}
AppPublisher=Modena AEC
AppPublisherURL=https://modena-aec.co.za
AppSupportURL=https://modena-aec.co.za/contact
AppUpdatesURL=https://modena-aec.co.za/contact
AppCopyright=Copyright (c) 2026 Modena AEC
DefaultDirName={pf}\Modena\ModelHealthChecker
DefaultGroupName=Modena AEC\Model Health Checker
OutputDir=installer-output
OutputBaseFilename=ModenaModelHealthChecker-Setup-{#AppVersion}
SetupIconFile=Modena-Icon.ico
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
Compression=lzma
SolidCompression=yes
UninstallDisplayIcon={app}\Modena.RevitAddin.dll
DisableProgramGroupPage=yes
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
; Create application directory
Name: "{app}"

[Files]
; Package payloads into installer (from build-multi-version.ps1 output)
Source: "..\publish\Installer\Modern\*"; DestDir: "{app}\Payload\Modern"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "..\publish\Installer\Legacy\*"; DestDir: "{app}\Payload\Legacy"; Flags: recursesubdirs createallsubdirs ignoreversion

; Deploy Modern payload to Revit 2025-2026 AddIns (if installed)
Source: "..\publish\Installer\Modern\*"; DestDir: "{code:GetRevitAddInsPath|2025}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "Modena.RevitAddin.addin;*.pdb"; Check: IsRevitInstalled('2025')
Source: "..\publish\Installer\Modern\*"; DestDir: "{code:GetRevitAddInsPath|2026}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "Modena.RevitAddin.addin;*.pdb"; Check: IsRevitInstalled('2026')

; Deploy Legacy payload to Revit 2024 AddIns (if installed)
Source: "..\publish\Installer\Legacy\*"; DestDir: "{code:GetRevitAddInsPath|2024}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "Modena.RevitAddin.addin;*.pdb"; Check: IsRevitInstalled('2024')

[InstallDelete]
; Remove pre-existing addin manifests before installing (clears dev-deployment manifests)
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\Modena.RevitAddin*.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\Modena.RevitAddin*.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\Modena.RevitAddin*.addin"
; Remove existing modena-config.json per version before installing new configuration
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\modena-config.json"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\modena-config.json"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\modena-config.json"

[UninstallDelete]
; Remove Model Health Checker files from all Revit AddIns folders
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\Modena.RevitAddin*"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\Modena.Shared*"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\Newtonsoft.Json*"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\modena-config.json"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\Modena.RevitAddin*"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\Modena.Shared*"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\Newtonsoft.Json*"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\modena-config.json"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\Modena.RevitAddin*"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\Modena.Shared*"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\Newtonsoft.Json*"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\modena-config.json"
; Remove empty log directory if it exists
Type: dirifempty; Name: "{userappdata}\ModenaModelHealthChecker\logs"
Type: dirifempty; Name: "{userappdata}\ModenaModelHealthChecker"

[Code]
{ ============================================================================
  REVIT PROCESS CHECK - must be closed before DLLs can be replaced
  ============================================================================ }

function IsRevitProcessRunning(): Boolean;
var
  TempFile: String;
  ResultCode: Integer;
  Lines: TArrayOfString;
  i: Integer;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\revit_procs.txt');

  Exec(ExpandConstant('{sys}\cmd.exe'),
    '/C tasklist /FI "IMAGENAME eq Revit.exe" /NH /FO CSV > "' + TempFile + '" 2>&1',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if LoadStringsFromFile(TempFile, Lines) then
  begin
    for i := 0 to GetArrayLength(Lines) - 1 do
    begin
      if Pos('Revit.exe', Lines[i]) > 0 then
      begin
        Result := True;
        Break;
      end;
    end;
  end;
  DeleteFile(TempFile);
end;

{ ============================================================================
  REVIT VERSION DETECTION
  ============================================================================ }

function GetRevitAddInsPath(Version: String): String;
begin
  Result := ExpandConstant('{userappdata}') + '\Autodesk\Revit\Addins\' + Version;
end;

function TryGetRevitExePath(Version: String; var ExePath: String): Boolean;
var
  key, installDir: String;
begin
  Result := False;
  installDir := '';

  key := 'SOFTWARE\Autodesk\Revit\Autodesk Revit ' + Version;
  if not RegQueryStringValue(HKLM, key, 'InstallLocation', installDir) then
    RegQueryStringValue(HKLM, key, 'InstallationLocation', installDir);

  if (installDir = '') then
  begin
    key := 'SOFTWARE\WOW6432Node\Autodesk\Revit\Autodesk Revit ' + Version;
    if not RegQueryStringValue(HKLM, key, 'InstallLocation', installDir) then
      RegQueryStringValue(HKLM, key, 'InstallationLocation', installDir);
  end;

  if (installDir = '') then
  begin
    key := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Autodesk Revit ' + Version;
    RegQueryStringValue(HKLM, key, 'InstallLocation', installDir);
  end;

  if (installDir <> '') then
  begin
    installDir := AddBackslash(installDir);
    ExePath := installDir + 'Revit.exe';
    if FileExists(ExePath) then
    begin
      Result := True;
      exit;
    end;
  end;

  ExePath := ExpandConstant('{pf64}') + '\Autodesk\Revit ' + Version + '\Revit.exe';
  if FileExists(ExePath) then
  begin
    Result := True;
    exit;
  end;

  Result := False;
end;

function IsRevitInstalled(Version: String): Boolean;
var
  exePath: String;
begin
  if TryGetRevitExePath(Version, exePath) then
  begin
    Result := True;
    exit;
  end;

  if DirExists(GetRevitAddInsPath(Version)) then
  begin
    Result := True;
    exit;
  end;

  Result := False;
end;

function GetInstalledRevitVersions: String;
var
  list: String;
begin
  list := '';
  if IsRevitInstalled('2024') then list := list + '2024, ';
  if IsRevitInstalled('2025') then list := list + '2025, ';
  if IsRevitInstalled('2026') then list := list + '2026, ';
  if (list <> '') then
    Delete(list, Length(list)-1, 2);
  Result := list;
end;

{ ============================================================================
  LIGHTWEIGHT OBFUSCATION HELPERS (XOR -> HEX)
  ============================================================================ }

const
  OBF_KEY = 'M0d3nA-MHC-01';

function HexNibble(n: Integer): Char;
begin
  if n < 10 then
    Result := Chr(Ord('0') + n)
  else
    Result := Chr(Ord('A') + (n - 10));
end;

function ByteToHex(b: Integer): String;
begin
  Result := HexNibble((b shr 4) and $F) + HexNibble(b and $F);
end;

function XorHex(const S, Key: String): String;
var
  i, klen: Integer;
  c, k: Char;
  b: Integer;
begin
  Result := '';
  if Key = '' then exit;
  klen := Length(Key);
  for i := 1 to Length(S) do
  begin
    c := S[i];
    k := Key[((i - 1) mod klen) + 1];
    b := Ord(c) xor Ord(k);
    Result := Result + ByteToHex(b);
  end;
end;

{ ============================================================================
  REVIT ADDINS FOLDER MANAGEMENT
  ============================================================================ }

procedure CreateRevitAddInsFolders;
begin
  if IsRevitInstalled('2024') then
    if not DirExists(GetRevitAddInsPath('2024')) then
      CreateDir(GetRevitAddInsPath('2024'));

  if IsRevitInstalled('2025') then
    if not DirExists(GetRevitAddInsPath('2025')) then
      CreateDir(GetRevitAddInsPath('2025'));

  if IsRevitInstalled('2026') then
    if not DirExists(GetRevitAddInsPath('2026')) then
      CreateDir(GetRevitAddInsPath('2026'));
end;

{ ============================================================================
  ADDIN MANIFEST & CONFIG GENERATION
  ============================================================================ }

function CreateAddinManifest(Version: String): Boolean;
var
  AddInsPath: String;
  AddinFilePath: String;
  AssemblyPath: String;
  Manifest: String;
  CfgPath: String;
  CfgJson: String;
  RawId: String;
  RawSecret: String;
  EncId: String;
  EncSecret: String;
begin
  AddInsPath := GetRevitAddInsPath(Version);
  AddinFilePath := AddInsPath + '\Modena.RevitAddin.addin';
  AssemblyPath := AddInsPath + '\Modena.RevitAddin.dll';

  Manifest :=
    '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
    '<RevitAddIns>' + #13#10 +
    '  <AddIn Type="Application">' + #13#10 +
    '    <Name>Modena Model Health Checker</Name>' + #13#10 +
    '    <Assembly>' + AssemblyPath + '</Assembly>' + #13#10 +
    '    <AddInId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</AddInId>' + #13#10 +
    '    <FullClassName>Modena.RevitAddin.App</FullClassName>' + #13#10 +
    '    <Text>Modena Model Health Checker</Text>' + #13#10 +
    '    <Description>Displays model health KPIs, failed checks, families, and element categories for the active Revit model. Part of the Modena AEC &amp; INFR suite.</Description>' + #13#10 +
    '    <VisibilityMode>AlwaysVisible</VisibilityMode>' + #13#10 +
    '    <Discipline>Any</Discipline>' + #13#10 +
    '    <VendorId>MDNA</VendorId>' + #13#10 +
    '    <VendorDescription>Modena AEC &amp; INFR - Architecture, Engineering &amp; Construction Software Solutions</VendorDescription>' + #13#10 +
    '  </AddIn>' + #13#10 +
    '</RevitAddIns>';

  Result := SaveStringToFile(AddinFilePath, Manifest, False);

  CfgPath := AddInsPath + '\modena-config.json';

  RawId := '{#ApsClientId}';
  RawSecret := '{#ApsClientSecret}';
  EncId := XorHex(RawId, OBF_KEY);
  EncSecret := XorHex(RawSecret, OBF_KEY);

  CfgJson := '{' + #13#10 +
             '  "RefreshIntervalMinutes": 5,' + #13#10 +
             '  "AutoRefreshEnabled": true,' + #13#10 +
             '  "RequestTimeoutSeconds": 30,' + #13#10 +
             '  "LoggingLevel": "Information",' + #13#10 +
             '  "ApsBaseUrl": "https://developer.api.autodesk.com",' + #13#10 +
             '  "ALG": "xor-hex",' + #13#10 +
             '  "APS_CLIENT_ID_X": "' + EncId + '",' + #13#10 +
             '  "APS_CLIENT_SECRET_X": "' + EncSecret + '"' + #13#10 +
             '}';
  SaveStringToFile(CfgPath, CfgJson, False);
end;

procedure DeployToAllRevitVersions;
begin
  if IsRevitInstalled('2024') then
    CreateAddinManifest('2024');
  if IsRevitInstalled('2025') then
    CreateAddinManifest('2025');
  if IsRevitInstalled('2026') then
    CreateAddinManifest('2026');
end;

{ ============================================================================
  INSTALLATION STEPS
  ============================================================================ }

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssInstall:
    begin
      if IsRevitProcessRunning() then
      begin
        MsgBox('Revit is currently running.' + #13#10 + #13#10 +
               'Model Health Checker cannot replace the add-in DLLs while Revit has them loaded.' + #13#10 +
               'Please close ALL Revit windows and try running the installer again.',
               mbCriticalError, MB_OK);
        Abort();
      end;
      CreateRevitAddInsFolders;
    end;

    ssPostInstall:
    begin
      DeployToAllRevitVersions;
    end;
  end;
end;

{ ============================================================================
  WIZARD PAGE CUSTOMIZATION
  ============================================================================ }

procedure InitializeWizard;
var
  InstalledVersions: String;
begin
  InstalledVersions := GetInstalledRevitVersions;

  if InstalledVersions = '' then
  begin
    MsgBox('No supported Revit versions (2024-2026) detected on this system.' + #13#10 +
           'Modena Model Health Checker requires Revit 2024 or later.' + #13#10 + #13#10 +
           'Installation will continue, but the add-in will not be available until Revit is installed.',
           mbInformation, MB_OK);
  end
  else
  begin
    MsgBox('Modena Model Health Checker will be installed for the following Revit versions:' + #13#10 +
           InstalledVersions + #13#10 + #13#10 +
           'Click OK to continue with installation.',
           mbInformation, MB_OK);
  end;
end;

{ ============================================================================
  UNINSTALLATION SUPPORT
  ============================================================================ }

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
    begin
      MsgBox('Modena Model Health Checker will be removed from all installed Revit versions.' + #13#10 +
             'Please restart Revit for changes to take effect.',
             mbInformation, MB_OK);
    end;
  end;
end;
