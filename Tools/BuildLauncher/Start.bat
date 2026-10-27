@echo off
title Battle Cities Builder
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Launcher.ps1" %*
if errorlevel 1 pause
