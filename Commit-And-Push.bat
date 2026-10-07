@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Publishing\Commit-And-Push.ps1" %*
set "BATTLECITIES_EXIT_CODE=%ERRORLEVEL%"
echo.
if not defined BATTLECITIES_NO_PAUSE pause
exit /b %BATTLECITIES_EXIT_CODE%
