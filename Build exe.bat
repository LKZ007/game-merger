@echo off
REM Builds Frankenstein.exe in dist\Frankenstein (the release workflow does the same plus the installer).
cd /d "%~dp0"
set PY=py
where py >nul 2>nul || set PY=python
%PY% -m pip install --upgrade pyinstaller -r requirements.txt
%PY% -m PyInstaller --noconfirm --onedir --windowed --noupx --name Frankenstein --icon installer\Frankenstein.ico --add-data "gamemerger\recipes;gamemerger\recipes" --add-data "gamemerger\web;gamemerger\web" Frankenstein.pyw
echo.
echo Done. Your app is at dist\Frankenstein\Frankenstein.exe
pause
