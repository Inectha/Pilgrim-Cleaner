@echo off
if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    "%ProgramFiles%\dotnet\dotnet.exe" %*
    exit /b %ERRORLEVEL%
)
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" %*
    exit /b %ERRORLEVEL%
)
echo [.NET Not Found] Please install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
exit /b 1
