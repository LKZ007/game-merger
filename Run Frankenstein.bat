@echo off
cd /d "%~dp0"
set PY=py
where py >nul 2>nul || set PY=python
if not exist ".deps-installed" (
  echo Installing what Frankenstein needs, one time only...
  %PY% -m pip install --upgrade -r requirements.txt && echo ok> ".deps-installed"
)
start "" %PY%w Frankenstein.pyw
