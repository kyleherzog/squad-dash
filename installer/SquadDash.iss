; SquadDash Inno Setup 6 installer script
; Build with: ISCC.exe /DAppVersion=1.0.0 SquadDash.iss
; Or use:     .\installer\build-installer.ps1 -Version 1.0.0

#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif

[Setup]
AppId={{52769C8B-FFBC-4427-9DA0-BBF6288CA206}
AppName=SquadDash
AppVersion={#AppVersion}
AppPublisher=SquadDash Team
AppPublisherURL=https://github.com/MillerMark/squad-dash
AppSupportURL=https://github.com/MillerMark/squad-dash/issues
AppUpdatesURL=https://github.com/MillerMark/squad-dash/releases
DefaultDirName={localappdata}\SquadDash
DefaultGroupName=SquadDash
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=SquadDash-{#AppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
; No UAC prompt — installs entirely within %LocalAppData%
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "shellmenu";   Description: "Add ""Open SquadDash Here"" to Windows Explorer folder context menu"; GroupDescription: "Shell integration"

[Files]
; Launcher — lives at the app root so the Start Menu shortcut points to it
Source: "..\artifacts\publish\launcher\SquadDash.exe"; DestDir: "{app}"; Flags: ignoreversion

; App payload — SquadDash.App.exe + all DLLs, runtimes, and assets from dotnet publish
Source: "..\artifacts\publish\app\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs

; Squad.SDK — Node.js runtime scripts + production node_modules
; NOTE: Node.js itself is NOT bundled. node.exe must be on PATH (see README / WinGet prerequisites).
Source: "..\artifacts\publish\sdk\*"; DestDir: "{app}\Squad.SDK"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SquadDash";                        Filename: "{app}\SquadDash.exe"
Name: "{group}\{cm:UninstallProgram,SquadDash}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\SquadDash";                  Filename: "{app}\SquadDash.exe"; Tasks: desktopicon

[Registry]
; Explorer context menu: right-click on a folder in the tree / file list
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SquadDash";          ValueType: string; ValueName: "";      ValueData: "Open SquadDash Here";                                        Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SquadDash";          ValueType: string; ValueName: "Icon";  ValueData: "{app}\SquadDash.exe,0";                                      Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SquadDash\command";  ValueType: string; ValueName: "";      ValueData: """{app}\SquadDash.exe"" ""--folder"" ""%1""";                Tasks: shellmenu

; Explorer context menu: right-click on the background of an open folder (%V = current folder)
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SquadDash";          ValueType: string; ValueName: "";      ValueData: "Open SquadDash Here";                            Flags: uninsdeletekey; Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SquadDash";          ValueType: string; ValueName: "Icon";  ValueData: "{app}\SquadDash.exe,0";                          Tasks: shellmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SquadDash\command";  ValueType: string; ValueName: "";      ValueData: """{app}\SquadDash.exe"" ""--folder"" ""%V""";   Tasks: shellmenu

[Run]
Filename: "{app}\SquadDash.exe"; Description: "{cm:LaunchProgram,SquadDash}"; Flags: nowait postinstall skipifsilent

; ---------------------------------------------------------------------------
; [Code] — prerequisite checks run before the installer completes
; ---------------------------------------------------------------------------
[Code]

// ---------------------------------------------------------------------------
// IsDotNet10DesktopRuntimeInstalled
//
// Checks whether the .NET 10 Windows Desktop Runtime (x64) is present by
// scanning the shared-framework directory for any 10.x.x sub-folder.
// SquadDash.App.exe requires Microsoft.WindowsDesktop.App >= 10.0.
// ---------------------------------------------------------------------------
function IsDotNet10DesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
  SearchPattern: String;
begin
  Result := False;
  SearchPattern := ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*');
  if FindFirst(SearchPattern, FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Result := True;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

// ---------------------------------------------------------------------------
// InitializeSetup
//
// Called before the installer UI appears.  If the required .NET runtime is
// absent the user is warned with a download URL.  Returning False cancels the
// install; returning True lets it proceed (user may plan to install .NET
// afterwards).
// ---------------------------------------------------------------------------
function InitializeSetup: Boolean;
var
  DownloadUrl: String;
begin
  Result := True;
  DownloadUrl := 'https://dotnet.microsoft.com/download/dotnet/10.0';

  if not IsDotNet10DesktopRuntimeInstalled then
  begin
    if MsgBox(
      '.NET 10 Desktop Runtime (x64) is not installed on this machine.' + #13#10 +
      'SquadDash requires it to run — the app will not launch until it is installed.' + #13#10#13#10 +
      'After this installer finishes, download and install the runtime from:' + #13#10 +
      DownloadUrl + #13#10#13#10 +
      'Continue with the SquadDash installation anyway?',
      mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;
