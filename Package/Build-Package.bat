@echo off
rem Vendor package build: double-click to run Build-Package.ps1 (output: dist\ValidationStage_SDK_v*.zip)
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Package.ps1"
if errorlevel 1 (
    echo.
    echo [FAILED] Package build failed. See the messages above.
    pause
    exit /b 1
)
echo.
echo [OK] Package created. Opening the dist folder...
start "" explorer "%~dp0..\dist"
pause
