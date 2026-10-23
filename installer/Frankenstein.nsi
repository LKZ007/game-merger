; Frankenstein installer. Build with:  makensis -DVER=3.1.0 installer\Frankenstein.nsi
; (after PyInstaller has made dist\Frankenstein\). Installs for the current user, so no admin
; prompt; adds desktop and Start menu shortcuts and an uninstaller in Apps & features.

Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"

!ifndef VER
  !define VER "0.0.0"
!endif
!define APP "Frankenstein"
!define EXE "Frankenstein.exe"
!define OLDAPP "Game Merger"  ; the app's name before 3.8.0
!define OLDEXE "GameMerger.exe"
!define PINS "$APPDATA\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameMerger"

Name "${APP}"
OutFile "..\dist\FrankensteinSetup.exe"
InstallDir "$LOCALAPPDATA\Programs\Frankenstein"
; installs from before the rename stay where they are
InstallDirRegKey HKCU "Software\GameMerger" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "${APP} ${VER}"

VIProductVersion "${VER}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "FileDescription" "${APP} Setup"
VIAddVersionKey "FileVersion" "${VER}"
VIAddVersionKey "ProductVersion" "${VER}"
VIAddVersionKey "CompanyName" "Frankenstein"
VIAddVersionKey "LegalCopyright" "Frankenstein"

!define MUI_ICON "Frankenstein.ico"
!define MUI_UNICON "Frankenstein.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "header.bmp"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TITLE "Alright! Let's get Frankenstein on your PC"
!define MUI_WELCOMEPAGE_TEXT "This installs Frankenstein ${VER}, the launcher that stitches your games together.$\r$\n$\r$\nYou'll get a desktop shortcut and a Start menu entry. Right-click either one and choose Pin to taskbar to keep it there.$\r$\n$\r$\nClick Install to carry on."
!define MUI_FINISHPAGE_TITLE "Sorted. Frankenstein is installed"
!define MUI_FINISHPAGE_TEXT "Open it from your desktop or the Start menu. Updates install themselves from now on."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Open Frankenstein now"

!define MUI_PAGE_CUSTOMFUNCTION_SHOW WelcomeInstallButton
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!define MUI_UNCONFIRMPAGE_TEXT_TOP "This removes the Frankenstein app. Your merged game copies, their saves and your settings are kept, so reinstalling picks them back up."
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

Section "Frankenstein"
  ; let the running app close itself (updates), then make sure it's gone
  StrCmp $Update 1 0 +2
    Sleep 2500
  nsExec::Exec 'taskkill /IM "${EXE}" /F'
  nsExec::Exec 'taskkill /IM "${OLDEXE}" /F'
  Sleep 500

  SetOutPath "$INSTDIR"
  RMDir /r "$INSTDIR\_internal"
  File /r "..\dist\Frankenstein\*"
  ; the icon file is named per version: Windows caches icons by file path, so a new name makes shortcuts
  ; show a changed logo straight away instead of the old one from the cache
  Delete "$INSTDIR\Frankenstein*.ico"
  File "/oname=Frankenstein-${VER}.ico" "Frankenstein.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; moving over from Game Merger: swap the old exe and shortcuts for the new ones
  Delete "$INSTDIR\${OLDEXE}"
  Delete "$INSTDIR\GameMerger.ico"
  Delete "$SMPROGRAMS\${OLDAPP}.lnk"
  StrCpy $0 0
  IfFileExists "$DESKTOP\${OLDAPP}.lnk" 0 +3
    Delete "$DESKTOP\${OLDAPP}.lnk"
    StrCpy $0 1
  ; a pin keeps working when its shortcut is rewritten in place
  IfFileExists "${PINS}\${OLDAPP}.lnk" 0 +2
    CreateShortcut "${PINS}\${OLDAPP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\Frankenstein-${VER}.ico" 0 SW_SHOWNORMAL "" "Merge your games with mods"
  IfFileExists "${PINS}\${APP}.lnk" 0 +2
    CreateShortcut "${PINS}\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\Frankenstein-${VER}.ico" 0 SW_SHOWNORMAL "" "Merge your games with mods"

  IfFileExists "$DESKTOP\${APP}.lnk" 0 +2
    StrCpy $0 1  ; an update rewrites your desktop shortcut too, so it gets the new icon
  StrCmp $Update 0 make_desktop
  StrCmp $0 1 make_desktop shortcuts_done
  make_desktop:
    CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\Frankenstein-${VER}.ico" 0 SW_SHOWNORMAL "" "Merge your games with mods"
  shortcuts_done:
  CreateShortcut "$SMPROGRAMS\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\Frankenstein-${VER}.ico" 0 SW_SHOWNORMAL "" "Merge your games with mods"

  ; tell Explorer the icons changed, and refresh its icon cache
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
  IfFileExists "$SYSDIR\ie4uinit.exe" 0 +2
    nsExec::Exec '"$SYSDIR\ie4uinit.exe" -show'

  WriteRegStr HKCU "Software\GameMerger" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${VER}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "Frankenstein"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\Frankenstein-${VER}.ico"
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
  Delete "$DESKTOP\${OLDAPP}.lnk"
  Delete "$SMPROGRAMS\${OLDAPP}.lnk"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${UNKEY}"
  DeleteRegKey HKCU "Software\GameMerger"
  DeleteRegKey HKCU "Software\Classes\.gmerge"
  DeleteRegKey HKCU "Software\Classes\GameMerger.Share"
  ; your merges, saves and settings in %LOCALAPPDATA%\GameMerger are kept, so reinstalling picks them back up
SectionEnd
