@echo off
setlocal
set "preview=%~dp0..\..\clients\unity\Open-Preview.ps1"
set "player=%~dp0..\..\artifacts\material-destruction-ribbon\Windows\AI-RTS-Spectator.exe"
if exist "%preview%" if exist "%player%" (
    powershell.exe -ExecutionPolicy RemoteSigned -File "%preview%" -Player "%player%"
    exit /b
)
if exist "%~dp0Output\Windows\AI-RTS-Spectator.exe" (
    start "" "%~dp0Output\Windows\AI-RTS-Spectator.exe"
    exit /b
)
echo Build the sample with Run.ps1 -Action Build first.
pause
