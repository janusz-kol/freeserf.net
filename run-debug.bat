@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-debug.ps1"
set "runExitCode=%errorlevel%"
echo.
if not "%runExitCode%"=="0" echo Build or game exited with code %runExitCode%.
echo Logs are in "%~dp0logs".
pause
exit /b %runExitCode%
