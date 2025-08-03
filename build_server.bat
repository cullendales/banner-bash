@echo off
echo Building Banner Bash Server...

cd servercode\GameServer

echo Cleaning previous build...
if exist bin\Release rmdir /s /q bin\Release
if exist obj\Release rmdir /s /q obj\Release

echo Building server...
dotnet build -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Server executable: servercode\GameServer\bin\Release\net8.0\GameServer.exe
    echo.
    echo To run the server:
    echo cd servercode\GameServer\bin\Release\net8.0
    echo GameServer.exe
    echo.
) else (
    echo.
    echo Build failed! Check the error messages above.
    echo.
)

pause 