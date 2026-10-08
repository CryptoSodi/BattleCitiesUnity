@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Publishing\Deploy-TestWeb.ps1" %*
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" echo Test web deployment failed. Read the error above.
pause
exit /b %RESULT%
