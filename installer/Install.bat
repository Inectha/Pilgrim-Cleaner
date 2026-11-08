@echo off
title Installing Pilgrim Cleaner...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-PilgrimCleaner.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Installation failed with error code %ERRORLEVEL%.
    pause
)
