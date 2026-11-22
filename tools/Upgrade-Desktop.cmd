@echo off
setlocal
title BIG BROTHER Desktop - Upgrade Existing App
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Upgrade-Desktop.ps1"
exit /b %ERRORLEVEL%
