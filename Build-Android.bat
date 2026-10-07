@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Publishing\Build-Android.ps1" %*
set "BATTLECITIES_EXIT_CODE=%ERRORLEVEL%"
echo.
if not defined BATTLECITIES_NO_PAUSE pause
exit /b %BATTLECITIES_EXIT_CODE%
