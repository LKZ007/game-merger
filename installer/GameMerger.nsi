; Game Merger installer. Build with:  makensis -DVER=3.1.0 installer\GameMerger.nsi
; (after PyInstaller has made dist\GameMerger\). Installs for the current user, so no admin
; prompt; adds desktop and Start menu shortcuts and an uninstaller in Apps & features.

Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"

!ifndef VER
  !define VER "0.0.0"
!endif
!define APP "Game Merger"
!define EXE "GameMerger.exe"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameMerger"

Name "${APP}"
OutFile "..\dist\GameMergerSetup.exe"
InstallDir "$LOCALAPPDATA\Programs\Game Merger"
InstallDirRegKey HKCU "Software\GameMerger" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "${APP} ${VER}"

VIProductVersion "${VER}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "FileDescription" "${APP} Setup"
VIAddVersionKey "FileVersion" "${VER}"
VIAddVersionKey "ProductVersion" "${VER}"
VIAddVersionKey "CompanyName" "Game Merger"
VIAddVersionKey "LegalCopyright" "Game Merger"

!define MUI_ICON "GameMerger.ico"
!define MUI_UNICON "GameMerger.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "header.bmp"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TITLE "Alright! Let's get Game Merger on your PC"
!define MUI_WELCOMEPAGE_TEXT "This installs Game Merger ${VER} with Franklin, your merge assistant.$\r$\n$\r$\nYou'll get a desktop shortcut and a Start menu entry. Right-click either one and choose Pin to taskbar to keep it there.$\r$\n$\r$\nClick Install to carry on."
!define MUI_FINISHPAGE_TITLE "Sorted. Game Merger is installed"
!define MUI_FINISHPAGE_TEXT "Open it from your desktop or the Start menu. Updates install themselves from now on."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Open Game Merger now"

!define MUI_PAGE_CUSTOMFUNCTION_SHOW WelcomeInstallButton
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!define MUI_UNCONFIRMPAGE_TEXT_TOP "This removes the Game Merger app. Your merged game copies, their saves and your settings are kept, so reinstalling picks them back up."
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Var Update

Function WelcomeInstallButton
  GetDlgItem $0 $HWNDPARENT 1
  SendMessage $0 ${WM_SETTEXT} 0 "STR:&Install"
FunctionEnd

Function .onInit
  StrCpy $Update 0
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/UPDATE" $R1
  IfErrors +2 0
    StrCpy $Update 1
FunctionEnd

Section "Game Merger"
  ; let the running app close itself (updates), then make sure it's gone
  StrCmp $Update 1 0 +2
    Sleep 2500
  nsExec::Exec 'taskkill /IM "${EXE}" /F'
  Sleep 500

  SetOutPath "$INSTDIR"
  RMDir /r "$INSTDIR\_internal"
  File /r "..\dist\GameMerger\*"
  File "GameMerger.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  StrCmp $Update 1 shortcuts_done
    CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\GameMerger.ico" 0 SW_SHOWNORMAL "" "Merge your games with mods"
  shortcuts_done:
  CreateShortcut "$SMPROGRAMS\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\GameMerger.ico" 0 SW_SHOWNORMAL "" "Merge your games with mods"

  WriteRegStr HKCU "Software\GameMerger" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${VER}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "Game Merger"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\GameMerger.ico"
  WriteRegStr HKCU "${UNKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "${UNKEY}" "EstimatedSize" $0

  ; silent updates reopen the app when they finish
  StrCmp $Update 1 0 +2
    Exec '"$INSTDIR\${EXE}"'
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /IM "${EXE}" /F'
  Sleep 500
  Delete "$DESKTOP\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}.lnk"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${UNKEY}"
  DeleteRegKey HKCU "Software\GameMerger"
  DeleteRegKey HKCU "Software\Classes\.gmerge"
  DeleteRegKey HKCU "Software\Classes\GameMerger.Share"
  ; your merges, saves and settings in %LOCALAPPDATA%\GameMerger are kept, so reinstalling picks them back up
SectionEnd
