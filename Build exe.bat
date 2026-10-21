@echo off
REM Builds a single GameMerger.exe in the dist folder.
cd /d "%~dp0"
set PY=py
where py >nul 2>nul || set PY=python
%PY% -m pip install --upgrade pyinstaller -r requirements.txt
%PY% -m PyInstaller --noconfirm --onefile --windowed --name GameMerger --icon gamemerger\web\franklin.ico --add-data "gamemerger\recipes;gamemerger\recipes" --add-data "gamemerger\web;gamemerger\web" GameMerger.pyw
echo.
echo Done. Your app is at dist\GameMerger.exe
pause
