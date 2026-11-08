@echo off
title Launching Pilgrim Cleaner...

if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%ProgramFiles%\dotnet"
    set "PATH=%ProgramFiles%\dotnet;%PATH%"
) else if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
    set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
)

if exist "%~dp0bin\Release\net8.0-windows\PilgrimCleaner.exe" (
    start "" "%~dp0bin\Release\net8.0-windows\PilgrimCleaner.exe"
    exit /b
)

if exist "%~dp0bin\Debug\net8.0-windows\PilgrimCleaner.exe" (
    start "" "%~dp0bin\Debug\net8.0-windows\PilgrimCleaner.exe"
    exit /b
)

echo PilgrimCleaner.exe not found in bin\Release or bin\Debug.
echo Compiling Pilgrim Cleaner...
call "%~dp0dotnet.cmd" build "%~dp0PilgrimCleaner.csproj" -c Release
if exist "%~dp0bin\Release\net8.0-windows\PilgrimCleaner.exe" (
    start "" "%~dp0bin\Release\net8.0-windows\PilgrimCleaner.exe"
) else (
    echo Build failed. Press any key to exit.
    pause
)
