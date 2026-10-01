@echo off
setlocal
cd /d "%~dp0"

rem A running instance locks the exe and blocks publish - close it first
taskkill /F /IM XboxControllerBatteryMonitor.exe >nul 2>&1
if errorlevel 1 (
    echo [1/3] No running instance found
) else (
    echo [1/3] Closed running XboxControllerBatteryMonitor
)

rem Wipe old output so the folder only contains this build's exe
if exist "publish" (
    echo [2/3] Cleaning old publish folder ...
    rmdir /s /q "publish"
)

echo [3/3] Publishing single-file exe ...
dotnet publish src\XboxBatteryMonitor -p:PublishProfile=src\XboxBatteryMonitor\Properties\PublishProfiles\FolderProfile.pubxml
if errorlevel 1 (
    echo.
    echo FAILED - see errors above.
    exit /b 1
)

echo.
echo OK: %~dp0publish\XboxControllerBatteryMonitor.exe
dir /b publish
