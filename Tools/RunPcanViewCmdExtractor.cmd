@echo off
setlocal

rem Run the extractor script with ExecutionPolicy Bypass and pass through args.
rem Examples:
rem   RunPcanViewCmdExtractor.cmd cadapt.trc
rem   RunPcanViewCmdExtractor.cmd "C:\path\to\cadapt.trc"

set "SCRIPT_DIR=%~dp0"
set "PS1=%SCRIPT_DIR%PcanViewCmdExtractor.ps1"

if not exist "%PS1%" (
  echo Script not found: "%PS1%"
  echo Put this .cmd file in the same folder as PcanViewCmdExtractor.ps1
  exit /b 1
)

set "INPUT=%~1"

if "%INPUT%"=="" (
  for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; $d.Title='Select .trc log'; $d.Filter='Trace files (*.trc)|*.trc|All files (*.*)|*.*'; $d.InitialDirectory=(Resolve-Path '%SCRIPT_DIR%').Path; if ($d.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 1 } ; Write-Output $d.FileName"`) do set "INPUT=%%I"
)

if "%INPUT%"=="" (
  exit /b 2
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -InputPath "%INPUT%"

if errorlevel 1 (
  echo.
  echo Failed with exit code %errorlevel%.
  pause
)
