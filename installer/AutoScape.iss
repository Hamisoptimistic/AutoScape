; ==============================================================================
; AutoScape - Professional Inno Setup Script
; Clean Native Modern Windows User-Level Installer
; ==============================================================================

#ifndef MyAppVersion
#define MyAppVersion "2.0.0"
#endif

#define MyAppName "AutoScape"
#define MyAppPublisher "AutoScape Team"
#define MyAppURL "https://github.com/Hamisoptimistic/Bing-Wallpaper"
#define MyAppExeName "Bing-Wallpaper-UI.ps1"

[Setup]
AppId={{D6F9C2B4-7A39-44F1-8B2E-319FA9C0E28A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Per-User Installation: Zero Admin/UAC prompts required
DefaultDirName={localappdata}\Programs\AutoScape
DisableDirPage=no
UsePreviousAppDir=no
UsePreviousTasks=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=..\dist
OutputBaseFilename=AutoScape-Setup
SetupIconFile=..\core\assets\app.ico
UninstallDisplayIcon={app}\core\assets\app.ico

; High-efficiency compression
Compression=lzma2/max
SolidCompression=yes

; Modern Windows styling
WizardStyle=modern

; Zero admin rights needed
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible

; Safety: Gracefully prompt & close running instances before file write
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenuicon"; Description: "Create a &Start Menu shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a &Desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "startupicon"; Description: "Launch AutoScape automatically on Windows startup (minimized)"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Package all application files from core\
Source: "..\core\*"; DestDir: "{app}\core"; Excludes: "*.bak,*.tmp,*.log"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu Shortcut (64-bit: Headless conhost avoids Windows Terminal popup)
Name: "{userprograms}\AutoScape"; Filename: "{sys}\conhost.exe"; \
    Parameters: "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"""; \
    WorkingDir: "{app}\core"; IconFilename: "{app}\core\assets\app.ico"; AppUserModelID: "AutoScape.App"; Tasks: startmenuicon; Check: Is64BitInstallMode
Name: "{userprograms}\AutoScape"; Filename: "powershell.exe"; \
    Parameters: "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"""; \
    WorkingDir: "{app}\core"; IconFilename: "{app}\core\assets\app.ico"; AppUserModelID: "AutoScape.App"; Tasks: startmenuicon; Check: not Is64BitInstallMode

; Desktop Shortcut (64-bit: Headless conhost avoids Windows Terminal popup)
Name: "{userdesktop}\AutoScape"; Filename: "{sys}\conhost.exe"; \
    Parameters: "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"""; \
    WorkingDir: "{app}\core"; IconFilename: "{app}\core\assets\app.ico"; AppUserModelID: "AutoScape.App"; Tasks: desktopicon; Check: Is64BitInstallMode
Name: "{userdesktop}\AutoScape"; Filename: "powershell.exe"; \
    Parameters: "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"""; \
    WorkingDir: "{app}\core"; IconFilename: "{app}\core\assets\app.ico"; AppUserModelID: "AutoScape.App"; Tasks: desktopicon; Check: not Is64BitInstallMode

; Windows Startup Shortcut (Optional User Opt-In)
Name: "{userstartup}\AutoScape"; Filename: "{sys}\conhost.exe"; \
    Parameters: "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"" -AutoApply"; \
    WorkingDir: "{app}\core"; IconFilename: "{app}\core\assets\app.ico"; AppUserModelID: "AutoScape.App"; Tasks: startupicon; Check: Is64BitInstallMode
Name: "{userstartup}\AutoScape"; Filename: "powershell.exe"; \
    Parameters: "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"" -AutoApply"; \
    WorkingDir: "{app}\core"; IconFilename: "{app}\core\assets\app.ico"; AppUserModelID: "AutoScape.App"; Tasks: startupicon; Check: not Is64BitInstallMode

[Run]
; 64-bit Windows: Launch via conhost.exe --headless (64bit flag disables WOW64 redirection, avoids Windows Terminal popup)
Filename: "{sys}\conhost.exe"; \
    Parameters: "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"""; \
    WorkingDir: "{app}\core"; Description: "Launch AutoScape"; Flags: postinstall nowait skipifsilent unchecked 64bit; Check: Is64BitInstallMode

; 32-bit Windows fallback: Direct powershell launch
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{app}\core\Bing-Wallpaper-UI.ps1"""; \
    WorkingDir: "{app}\core"; Description: "Launch AutoScape"; Flags: postinstall nowait skipifsilent unchecked; Check: not Is64BitInstallMode

[Code]
// Initialize Wizard: Make destination location visible but completely muted / non-clickable
procedure InitializeWizard();
begin
  WizardForm.DirEdit.Enabled := False;
  WizardForm.DirEdit.TabStop := False;
  WizardForm.DirBrowseButton.Visible := False;
  WizardForm.DirBrowseButton.Enabled := False;
  WizardForm.SelectDirBrowseLabel.Caption := 'Click Next to continue.';
end;

// Smart Uninstaller: Prompt user to keep or purge downloaded wallpapers & settings
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\AutoScape');
    if DirExists(DataDir) then
    begin
      if MsgBox('Would you also like to delete your downloaded wallpaper cache, logs, and preferences?' + #13#10#13#10 +
                'Click [Yes] to completely remove all AutoScape data,' + #13#10 +
                'or [No] to keep your downloaded wallpapers on this PC.',
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
